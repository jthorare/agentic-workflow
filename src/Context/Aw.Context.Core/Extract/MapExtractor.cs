using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract.DataStores;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Extract;

/// <summary>Composes the recognizers over a repo's files at one commit (S-4): deterministic nodes, each with a file-level 'implements' edge from the file that defines it.</summary>
public static class MapExtractor
{
    /// <summary>The recognizers' version. A backtest's as-of map cache is keyed by it, so a change to what the recognizers find bumps it.</summary>
    public const int Version = 6;

    /// <summary>Extracts the map from the files.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="files">The files at the commit.</param>
    /// <returns>The nodes and edges, ordered, the same for the same files.</returns>
    public static ExtractedMap Extract(ContextRepoName repo, IReadOnlyList<RepoFile> files) => Extract(repo, files, RecognizerSet.None).Map;

    /// <summary>Extracts the map with the profile's recognizers added to the built-in table.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="files">The files at the commit.</param>
    /// <param name="recognizers">The profile's recognizers; their matches are suggested at confidence 0.8.</param>
    /// <param name="schemaGlobs">'context.code.schemaGlobs': the files that declare data stores; null means the defaults.</param>
    /// <returns>The map, and whether a recognizer timed out on some file.</returns>
    public static MapExtraction Extract(ContextRepoName repo, IReadOnlyList<RepoFile> files, RecognizerSet recognizers, IReadOnlyList<string>? schemaGlobs = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(recognizers);
        // Journey files feed the flow pass only; no other pass reads them.
        var (withActions, timedOut) = Source([.. files.Where(f => !Journeys.IsJourney(f.Path))], repo, recognizers, schemaGlobs);
        var (flows, skips) = FlowPass.Run(repo, [.. files.Where(f => Journeys.IsJourney(f.Path))], withActions);
        return new MapExtraction(withActions.Plus(flows), timedOut) { Skips = skips };
    }

    private static (ExtractedMap Map, bool TimedOut) Source(IReadOnlyList<RepoFile> files, ContextRepoName repo, RecognizerSet recognizers, IReadOnlyList<string>? schemaGlobs)
    {
        var (profile, timedOut) = recognizers.Find(files);
        var hits = files.Where(Recognizers.Source).OrderBy(f => f.Path, StringComparer.Ordinal)
            .SelectMany(file => Recognizers.All.Where(r => r.Applies(file)).SelectMany(r => r.Find(file)).Select(found => (File: file, Found: found)))
            .Concat(profile)
            .Where(h => CodeKey(h.File.Path) is not null && Evidence(repo, h.File.Path) is not null)
            .ToList();
        List<GraphNode> nodes = [.. hits.GroupBy(h => h.Found.Key.Value, StringComparer.Ordinal).Select(g => Node(repo, [.. g]))];
        List<GraphNode> codeNodes = [.. hits.Select(h => h.File.Path).Distinct(StringComparer.Ordinal).Select(CodeNode)];
        var known = nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var implements = hits.Select(h => new GraphEdge(CodeKey(h.File.Path)!.Value, h.Found.Key, EdgeType.Implements, Provenance.Deterministic, 1.0, [Evidence(repo, h.File.Path)!]));   // why: hits keep only files whose keys are valid
        var related = hits.SelectMany(h => h.Found.Edges.Where(e => known.Contains(e.To.Value)).Select(e => e with { Evidence = [Evidence(repo, h.File.Path)!] }));   // why: as above
        var imports = ImportEdges.Find(files).Pairs.Where(p => CodeKey(p.From) is not null && CodeKey(p.To) is not null).ToList();
        var importNodes = imports.SelectMany(p => new[] { p.From, p.To }).Except(codeNodes.Select(n => n.Key.Value[5..]), StringComparer.Ordinal).Distinct(StringComparer.Ordinal).Select(CodeNode);
        var depends = imports.Select(p => new GraphEdge(CodeKey(p.From)!.Value, CodeKey(p.To)!.Value, EdgeType.DependsOn, Provenance.Deterministic, 1.0, Evidence(repo, p.From) is { } e ? [e] : null));   // why: the filter above keeps valid keys
        var found = ExtractedMap.Empty.Plus(new ExtractedMap([.. nodes, .. codeNodes, .. importNodes], [.. implements, .. related, .. depends]));
        var withStores = found.Plus(DataStorePass.Run(repo, files, DataStoreAdapters.All, schemaGlobs ?? ContextSettings.DefaultSchemaGlobs));
        return (withStores.Plus(ActionPass.Run(repo, files, withStores)), timedOut);
    }

    private static GraphNode Node(ContextRepoName repo, List<(RepoFile File, Found Found)> hits)
    {
        var best = hits.OrderByDescending(h => h.Found.Named).ThenByDescending(h => h.Found.Confidence).First().Found;
        var evidence = hits.Select(h => h.File.Path).Distinct(StringComparer.Ordinal).Select(p => Evidence(repo, p)!).ToList();   // why: hits keep only files whose evidence keys are valid
        return new GraphNode(best.Key, best.Label, Provenance.Deterministic, best.Confidence, evidence);
    }

    internal static GraphNode CodeNode(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return new GraphNode(CodeKey(path)!.Value, name, Provenance.Deterministic);   // why: only files with valid keys get here
    }

    internal static NodeKey? CodeKey(string path) => NodeKey.TryFrom("code:" + path, out var key) ? key : null;

    internal static EvidenceRef? Evidence(ContextRepoName repo, string path) =>
        RecordKey.TryFrom($"repo:{repo.Value}/{path}", out var key) ? new EvidenceRef(key) : null;
}
