using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Labeling;

/// <summary>
/// Remembers what the labeler answered (q149: model calls happen at setup and on new data only). A cluster's key is the sha256 of the files the model would be shown, scrubbed
/// the way <see cref="ModelLabeler"/> scrubs them, never the cluster's positional id (one new slot renumbers every later one). Hits come back under the cluster's current id; only
/// misses go to the inner labeler, and what it names is saved. A cache that can't be read is all misses and one that can't be written still returns the labels: the build never fails on it.
/// </summary>
/// <param name="inner">The labeler asked about the misses.</param>
/// <param name="cache">The saved labels.</param>
/// <param name="scrubbers">Builds the scrubbers the keys are made through.</param>
internal sealed class CachingLabeler(ILabeler inner, ILabelCache cache, IScrubberFactory scrubbers) : ILabeler
{
    public async ValueTask<Result<LabelPass>> Label(IReadOnlyList<FileCluster> clusters, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        var built = scrubbers.For(extraPatterns);
        if (built.IsError)
        {
            return built.Error;
        }

        var keys = clusters.Select(c => Key(c, built.Value.Egress)).ToList();
        var known = await cache.Find(keys, cancellationToken).ConfigureAwait(false);
        var saved = known.IsError ? new Dictionary<string, string>(StringComparer.Ordinal) : known.Value;

        var (hits, misses, missKeys) = Split(clusters, keys, saved);
        if (misses.Count == 0)
        {
            return new LabelPass(hits);
        }

        var asked = await inner.Label(misses, extraPatterns, maxTokens, cancellationToken).ConfigureAwait(false);
        if (asked.IsError)
        {
            // A model error with answers in hand is an incomplete pass, not a failed one.
            return hits.Count > 0 ? new LabelPass(hits, Complete: false) : asked.Error;
        }

        var named = asked.Value.Labels.Where(l => missKeys.ContainsKey(l.Id.Value)).ToList();
        if (named.Count > 0)
        {
            // best effort: a cache that can't be written costs the next build a model call, never this one its labels
            _ = await cache.Save([.. named.Select(l => KeyValuePair.Create(missKeys[l.Id.Value], l.Label))], cancellationToken).ConfigureAwait(false);
        }

        return asked.Value with { Labels = [.. hits, .. asked.Value.Labels] };
    }

    private static (List<ClusterLabel> Hits, List<FileCluster> Misses, Dictionary<string, string> MissKeys) Split(
        IReadOnlyList<FileCluster> clusters, List<string> keys, IReadOnlyDictionary<string, string> saved)
    {
        var hits = new List<ClusterLabel>();
        var misses = new List<FileCluster>();
        var missKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < clusters.Count; i++)
        {
            if (saved.TryGetValue(keys[i], out var label))
            {
                hits.Add(new ClusterLabel(clusters[i].Id, label));
            }
            else
            {
                misses.Add(clusters[i]);
                missKeys[clusters[i].Id.Value] = keys[i];
            }
        }

        return (hits, misses, missKeys);
    }

    internal static string Key(FileCluster cluster, IEgressScrubber egress)
    {
        var files = cluster.Files.Take(ModelLabeler.FilesPerCluster).Select(f => egress.Scrub(f).Text);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("label-v1\n" + string.Join('\n', files))));
    }
}
