using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Extract;

/// <summary>Turns clusters into candidate features (S-4): model-provenance feature nodes with 'implements' edges from their source files. Files the repo demotes, and manifest, lock and build-config files, keep their code node but implement no feature and never key, name or order one.</summary>
public static class ClusterFeatures
{
    /// <summary>Proposes a feature per cluster.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="clusters">What the cluster source found.</param>
    /// <param name="labels">Names the labeling pass gave some clusters, or none.</param>
    /// <param name="extracted">The deterministic extraction: a cluster claims the surfaces whose defining file it holds ('part-of').</param>
    /// <returns>The features, their file links and their claimed surfaces.</returns>
    public static ExtractedMap Propose(ContextRepoName repo, ClusterSet clusters, IReadOnlyList<ClusterLabel> labels, ExtractedMap extracted) =>
        Propose(repo, _ => false, clusters, labels, extracted);

    private static ExtractedMap Propose(ContextRepoName repo, Func<string, bool> demotes, ClusterSet clusters, IReadOnlyList<ClusterLabel> labels, ExtractedMap extracted)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(extracted);
        if (!clusters.Available)
        {
            return ExtractedMap.Empty;
        }

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var result = ExtractedMap.Empty;
        var split = clusters.Clusters.Select(c => Split(c, demotes)).ToList();
        foreach (var (cluster, files, demoted) in split.Where(c => c.Product.Count > 0).OrderBy(c => c.Product[0], StringComparer.Ordinal).ThenBy(c => c.Cluster.Id.Value, StringComparer.Ordinal))
        {
            var key = Unique(files, taken);
            var label = labels.FirstOrDefault(l => l.Id.Value == cluster.Id.Value)?.Label is { } named && !string.IsNullOrWhiteSpace(named)
                ? string.Join(' ', named.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                : Humanize(Leaf(files));
            result = result.Plus(One(new Candidate(repo, key, label, cluster.Confidence, files, demoted), extracted, claimed));
        }

        // A cluster of only set-aside files proposes no feature, but its files keep their code nodes.
        var orphans = split.Where(c => c.Product.Count == 0).SelectMany(c => c.Demoted).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(CodeNode).ToList();
        return orphans.Count == 0 ? result : result.Plus(new ExtractedMap(orphans, []));
    }

    /// <summary>Proposes a feature per cluster for a repo.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="clusters">What the cluster source found.</param>
    /// <param name="labels">Names the labeling pass gave some clusters, or none.</param>
    /// <param name="extracted">The deterministic extraction.</param>
    /// <returns>The features, their file links and their claimed surfaces.</returns>
    public static ExtractedMap Propose(ContextRepo repo, ClusterSet clusters, IReadOnlyList<ClusterLabel> labels, ExtractedMap extracted)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return Propose(repo.Name, repo.Demotes, clusters, labels, extracted);
    }

    private static ExtractedMap One(Candidate candidate, ExtractedMap extracted, HashSet<string> claimed)
    {
        var (repo, key, label, confidence, files, demoted) = candidate;
        var inCluster = files.ToHashSet(StringComparer.Ordinal);
        var nodes = new List<GraphNode> { new(key, label, Provenance.Model, confidence) };
        var edges = new List<GraphEdge>();
        foreach (var file in files)
        {
            var code = NodeKey.From("code:" + file);
            nodes.Add(new GraphNode(code, file[(file.LastIndexOf('/') + 1)..], Provenance.Deterministic));
            var evidence = RecordKey.TryFrom($"repo:{repo.Value}/{file}", out var record) ? new[] { new EvidenceRef(record) } : null;
            edges.Add(new GraphEdge(code, key, EdgeType.Implements, Provenance.Model, confidence, evidence));
        }

        nodes.AddRange(demoted.Select(CodeNode));
        var surfaces = extracted.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Value.StartsWith("code:", StringComparison.Ordinal) && inCluster.Contains(e.From.Value["code:".Length..]))
            .Select(e => e.To).Where(k => k.Kind == NodeKind.Surface && claimed.Add(k.Value)).OrderBy(k => k.Value, StringComparer.Ordinal);
        edges.AddRange(surfaces.Select(s => new GraphEdge(s, key, EdgeType.PartOf, Provenance.Model, confidence)));
        return new ExtractedMap(nodes, edges);
    }

    private static GraphNode CodeNode(string file) => new(NodeKey.From("code:" + file), file[(file.LastIndexOf('/') + 1)..], Provenance.Deterministic);

    private static (FileCluster Cluster, List<string> Product, List<string> Demoted) Split(FileCluster cluster, Func<string, bool> demotes)
    {
        var files = Files(cluster);
        bool SetAside(string f) => demotes(f) || ManifestFiles.Is(f);
        return (cluster, [.. files.Where(f => !SetAside(f))], [.. files.Where(SetAside)]);
    }

    // Distinct files with a usable code key, in path order.
    private static List<string> Files(FileCluster cluster) =>
        [.. cluster.Files.Where(f => NodeKey.TryFrom("code:" + f, out _)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    // The key comes from the files, never from the cluster's id or label, so a rebuild, a renumbering or a relabel keeps it (and the decisions made on it).
    // Files that share no directory are keyed by the first sorted file's directory (apps/a/index.ts -> apps.a); only a first file at the repo root falls back to its stem.
    private static NodeKey Unique(List<string> files, HashSet<string> taken)
    {
        var dir = KeyDirectory(files);
        var stem = Stem(files[0]);
        var candidates = dir.Length > 0
            ? new[] { dir.Replace('/', '.'), $"{dir.Replace('/', '.')}.{stem}" }
            : [stem];
        var numbered = dir.Length > 0 ? candidates[1] : stem;
        foreach (var name in candidates.Concat(Enumerable.Range(2, 1000).Select(n => $"{numbered}-{n.ToString(System.Globalization.CultureInfo.InvariantCulture)}")))
        {
            if (NodeKey.TryFrom("feature:" + name, out var key) && taken.Add(EntryKey.For(key).Value))
            {
                return EntryKey.For(key);
            }
        }

        throw new InvalidOperationException("no free feature key for a cluster");
    }

    // The shared directory, else the first file's own directory (empty only for a file at the repo root).
    private static string KeyDirectory(List<string> files)
    {
        var common = CommonDirectory(files);
        return common.Length > 0 ? common : DirectoryOf(files[0]);
    }

    private static string DirectoryOf(string file) => file.Contains('/', StringComparison.Ordinal) ? file[..file.LastIndexOf('/')] : "";

    private static string CommonDirectory(List<string> files)
    {
        var dirs = files.Select(f => f.Contains('/', StringComparison.Ordinal) ? f[..f.LastIndexOf('/')].Split('/') : []).ToList();
        var common = dirs[0].TakeWhile((segment, i) => dirs.TrueForAll(d => d.Length > i && string.Equals(d[i], segment, StringComparison.Ordinal)));
        return string.Join('/', common);
    }

    private static string Leaf(List<string> files)
    {
        var dir = KeyDirectory(files);
        return dir.Length > 0 ? dir[(dir.LastIndexOf('/') + 1)..] : Stem(files[0]);
    }

    private static string Stem(string file)
    {
        var name = file[(file.LastIndexOf('/') + 1)..];
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 ? name[..dot] : name;
    }

    private static string Humanize(string text)
    {
        var words = text.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ');
        return words.Length == 0 ? text : string.Concat(words[..1].ToUpper(System.Globalization.CultureInfo.InvariantCulture), words.AsSpan(1));
    }

    private sealed record Candidate(ContextRepoName Repo, NodeKey Key, string Label, double Confidence, List<string> Files, List<string> Demoted);
}
