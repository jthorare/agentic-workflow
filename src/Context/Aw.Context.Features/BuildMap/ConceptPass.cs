using Aw.Context.Core.Concepts;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.BuildMap;

/// <summary>
/// The concept step of a map build: reads the stored symbol vectors of the features' files, builds the glossary, derives the concepts with it (as one more grouping signal and as the terms
/// the namer and the answers carry) and names them. No vectors, or a code index that can't answer, leaves the concepts exactly as they were without a glossary; a read that fails says so as a note.
/// </summary>
/// <param name="namer">Names the derived concepts.</param>
/// <param name="vectors">The stored symbol embeddings.</param>
/// <param name="hintStore">The concept hints of the last scan.</param>
/// <param name="store">The map versions.</param>
internal sealed class ConceptPass(IConceptNamer namer, ISymbolVectors vectors, IConceptHintStore hintStore, IGraphStore store)
{
    // The code index answers at most this many files in one read.
    private const int MaxPaths = 2000;

    internal async ValueTask<ConceptOutcome> Run(ContextSettings settings, GraphSnapshot joined, IReadOnlyList<ConceptPin> pins, IReadOnlyList<ContextRepo> repos, CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var glossary = await Glossary(joined, repos, notes, cancellationToken).ConfigureAwait(false);
        ConceptNames? names = null;
        var hints = await Hints(cancellationToken).ConfigureAwait(false);
        var groups = ConceptDeriver.Groups(joined, pins, glossary, hints);
        if (settings.Labeling.Enabled && groups.Count > 0)
        {
            var named = await namer.Name(groups, settings.ExtraPatterns, settings.Labeling.MaxTokens, cancellationToken).ConfigureAwait(false);
            names = named.IsError ? null : named.Value;
            if (named.IsError)
            {
                notes.Add("concepts");
            }
            else if (named.Value.Unavailable is { } why)
            {
                notes.Add($"concepts: {why}");
            }
        }

        var concepts = ConceptDeriver.Derive(joined, pins, names, glossary, hints);
        return new ConceptOutcome(joined with
        {
            Nodes = [.. joined.Nodes.Concat(concepts.Nodes).DistinctBy(n => n.Key.Value, StringComparer.Ordinal)],
            Edges = [.. joined.Edges.Concat(concepts.Edges).DistinctBy(e => (e.From.Value, e.To.Value, e.Type))],
        }, notes);
    }

    // The concept hints of the newest map version the scan has looked at; a store that can't answer, or a version with none, shapes nothing.
    private async ValueTask<IReadOnlyList<ConceptHint>> Hints(CancellationToken cancellationToken)
    {
        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        if (versions.IsError || versions.Value.Count == 0)
        {
            return [];
        }

        var hints = await hintStore.Read(versions.Value[0].Number, cancellationToken).ConfigureAwait(false);
        return hints.IsError ? [] : hints.Value;
    }

    // The glossary of the features whose files have vectors, or null when none has; a repo whose vectors can't be read adds the note.
    private async ValueTask<MapGlossary?> Glossary(GraphSnapshot joined, IReadOnlyList<ContextRepo> repos, List<string> notes, CancellationToken cancellationToken)
    {
        var features = FeatureFiles.Of(joined);
        var paths = features.SelectMany(f => f.Paths).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (paths.Count == 0)
        {
            return null;
        }

        var (byPath, failed) = await Vectors(repos, paths, cancellationToken).ConfigureAwait(false);
        if (failed)
        {
            notes.Add("glossary");
        }

        if (byPath.Count == 0)
        {
            return null;
        }

        var built = GlossaryBuilder.Build([.. features.Select(f => new FeatureVectors(f.Feature, f.Label, [.. f.Paths.SelectMany(p => byPath.GetValueOrDefault(p) ?? [])]))], new GlossaryRules());
        return built.Clusters.Count == 0 ? null : built;
    }

    // Each repo is asked for the paths no earlier repo had vectors for, in reads of at most 2,000 files.
    private async ValueTask<(Dictionary<string, List<SymbolVector>> ByPath, bool Failed)> Vectors(IReadOnlyList<ContextRepo> repos, List<string> paths, CancellationToken cancellationToken)
    {
        var byPath = new Dictionary<string, List<SymbolVector>>(StringComparer.Ordinal);
        var failed = false;
        foreach (var repo in repos)
        {
            foreach (var chunk in paths.Where(p => !byPath.ContainsKey(p)).Chunk(MaxPaths))
            {
                var read = await vectors.Read(repo.Name, chunk, cancellationToken).ConfigureAwait(false);
                if (read.IsError)
                {
                    failed = true;
                    continue;
                }

                foreach (var found in read.Value.Vectors)
                {
                    if (!byPath.TryGetValue(found.Path, out var symbols))
                    {
                        byPath[found.Path] = symbols = [];
                    }

                    symbols.Add(found.Symbol);
                }
            }
        }

        return (byPath, failed);
    }
}
