using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Symbols;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>One read of the store for one build: the nodes and edges it finds and the counts the report prints. Not thread-safe; a build makes one per read.</summary>
internal sealed class RecordPass(IRecordStore records, ICodeSymbolSource symbols, MapRecordInput input, IReadOnlyList<StoredRecord> stored)
{
    private const int SymbolHitsAsked = 200;

    private static readonly RecordLinkKind[] _linkKinds = [RecordLinkKind.Parent, RecordLinkKind.Blocks, RecordLinkKind.Duplicates, RecordLinkKind.Relates, RecordLinkKind.Closes, RecordLinkKind.Attachment];

    private readonly RepoPaths _paths = new(input.Repos);
    private readonly Dictionary<string, StoredRecord> _live = stored.ToDictionary(r => r.Key.Value, StringComparer.Ordinal);
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<(string From, string To), GraphEdge> _edges = [];
    private readonly Dictionary<CitationOutcome, int> _cited = [];
    private readonly Dictionary<(string Repo, string Name), IReadOnlyList<(string Repo, string Path)>> _symbolFiles = [];
    private int _notSynced;
    private int _older;
    private int _removedTouches, _overCap, _overCapPaths;
    private readonly List<RolledRecord> _overflow = [];
    private readonly List<RolledRecord> _underCap = [];

    /// <summary>Reads the docs, then the tracker records.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The store's error when it can't be read, else null.</returns>
    internal async ValueTask<Error?> Run(CancellationToken cancellationToken)
    {
        foreach (var repo in input.Repos)
        {
            if (await Docs(repo, cancellationToken).ConfigureAwait(false) is { } failed)
            {
                return failed;
            }
        }

        foreach (var record in stored.Where(r => r.Source != RepoDocs.Id && r.Kind != RecordKind.Doc).OrderBy(r => r.Key.Value, StringComparer.Ordinal))
        {
            if (await Tracker(record, cancellationToken).ConfigureAwait(false) is { } failed)
            {
                return failed;
            }
        }

        return null;
    }

    /// <summary>What the pass found.</summary>
    /// <param name="stamp">The store's record stamp, read before the pass.</param>
    /// <returns>The map part and the report lines.</returns>
    internal MapRecordReading Reading(long stamp)
    {
        var map = new ExtractedMap(
            [.. _nodes.Values.OrderBy(n => n.Key.Value, StringComparer.Ordinal)],
            [.. _edges.Values.OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal)]);
        var (notes, degraded) = Notes(stamp);
        return new MapRecordReading(map, stamp, notes) { Degraded = degraded, Overflow = [.. _overflow.OrderBy(r => r.Node.Key.Value, StringComparer.Ordinal)], UnderCap = [.. _underCap.OrderBy(r => r.Node.Key.Value, StringComparer.Ordinal)] };
    }

    private async ValueTask<Error?> Docs(MapRepoFiles repo, CancellationToken cancellationToken)
    {
        foreach (var node in DocRecords.Find(repo.Name, repo.Paths, input.DocGlobs, input.DirectoryNames, input.At))
        {
            _nodes[node.Key.Value] = node;
            if (!_live.TryGetValue(RepoDocs.Key(repo.Name, node.Label).Value, out var doc))
            {
                _notSynced++;
            }
            else if (!doc.Blocked && !(repo.Blobs.TryGetValue(node.Label, out var blob) && string.Equals(doc.Version.Value, blob, StringComparison.Ordinal)))
            {
                _older++;
            }
            else if (!doc.Blocked)
            {
                var targets = new HashSet<(string Repo, string Path)>();
                if (await Cite(doc, targets, cancellationToken).ConfigureAwait(false) is { } failed)
                {
                    return failed;
                }

                _ = Emit(doc.Key, node, targets);
            }
        }

        return null;
    }

    private async ValueTask<Error?> Tracker(StoredRecord record, CancellationToken cancellationToken)
    {
        if (!NodeKey.TryFrom("record:" + record.Key.Value, out var from))
        {
            return null;
        }

        var targets = new HashSet<(string Repo, string Path)>();
        Touches(record, targets);
        if (!record.Blocked && await Cite(record, targets, cancellationToken).ConfigureAwait(false) is { } failed)
        {
            return failed;
        }

        var node = Node(record, from);
        if (Links(record, from) + Emit(record.Key, node, targets) > 0)
        {
            _ = _nodes.TryAdd(from.Value, node);
        }

        return null;
    }

    // The paths and symbols a record's text cites, resolved against the path lists: an exact match joins the record's file targets, anything else is a count.
    private async ValueTask<Error?> Cite(StoredRecord record, HashSet<(string Repo, string Path)> targets, CancellationToken cancellationToken)
    {
        var chunks = await records.Chunks(record.Key, cancellationToken).ConfigureAwait(false);
        if (chunks.IsError)
        {
            return chunks.Error;
        }

        var text = string.Join('\n', chunks.Value.Select(c => c.Text));
        foreach (var path in CitedPaths.Find(text))
        {
            var (outcome, holder) = _paths.Resolve(record.Repo, path);
            _cited[outcome] = _cited.GetValueOrDefault(outcome) + 1;
            if (holder is not null)
            {
                _ = targets.Add((holder, path));
            }
        }

        foreach (var name in CitedPaths.FindSymbols(text))
        {
            var files = await SymbolFiles(record.Repo, name, cancellationToken).ConfigureAwait(false);
            var outcome = files.Count switch { 0 => CitationOutcome.Missing, 1 => CitationOutcome.Resolved, _ => CitationOutcome.Ambiguous };
            _cited[outcome] = _cited.GetValueOrDefault(outcome) + 1;
            if (outcome == CitationOutcome.Resolved)
            {
                _ = targets.Add(files[0]);
            }
        }

        return null;
    }

    // The mapped files that define a symbol of this exact name, in the record's repo or (a record naming none) in every mapped repo, as (repo, path); two are enough to call it ambiguous. A code index that is off, doesn't know the name or can't be read answers none: the citation is counted, never guessed.
    private async ValueTask<IReadOnlyList<(string Repo, string Path)>> SymbolFiles(ContextRepoName? repo, string name, CancellationToken cancellationToken)
    {
        var key = (repo?.Value ?? string.Empty, name);
        if (_symbolFiles.TryGetValue(key, out var known))
        {
            return known;
        }

        var found = new List<(string Repo, string Path)>();
        foreach (var scope in input.Repos.Where(r => repo is not { } own || r.Name == own))
        {
            var hits = await symbols.FindSymbols(scope.Name, new SymbolQuery(name, Prefix: false, Max: SymbolHitsAsked), asOf: null, cancellationToken).ConfigureAwait(false);
            if (hits.IsError)
            {
                continue;
            }

            foreach (var path in hits.Value.Rows.Select(h => h.Site.Path).Where(p => _paths.Has(scope.Name.Value, p)).Distinct(StringComparer.Ordinal))
            {
                if (found.Count < 2 && !found.Contains((scope.Name.Value, path)))
                {
                    found.Add((scope.Name.Value, path));
                }
            }
        }

        return _symbolFiles[key] = found;
    }

    // Source-stated links between two stored records; a target the store doesn't hold, or a doc outside the path list, makes no edge.
    private int Links(StoredRecord record, NodeKey from)
    {
        var made = 0;
        foreach (var link in record.Links.Where(l => _linkKinds.Contains(l.Kind)))
        {
            if (link.ToKey is { } linked && _live.TryGetValue(linked.Value, out var target) && Target(target) is { } to)
            {
                made += AddEdge(new GraphEdge(from, to, EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(record.Key, link.Kind.ToString().ToLowerInvariant())])) ? 1 : 0;
            }
        }

        return made;
    }

    private NodeKey? Target(StoredRecord target)
    {
        if (!NodeKey.TryFrom("record:" + target.Key.Value, out var key))
        {
            return null;
        }

        if (target.Source == RepoDocs.Id)
        {
            return _nodes.ContainsKey(key.Value) ? key : null;
        }

        _ = _nodes.TryAdd(key.Value, Node(target, key));
        return key;
    }

    // A pull request's changed files that are in the path list join its file targets. A path since removed is a count of its own.
    private void Touches(StoredRecord record, HashSet<(string Repo, string Path)> targets)
    {
        foreach (var touched in record.Links.Where(l => l.Kind == RecordLinkKind.Touches).Select(l => _paths.Touched(l.ToRef)).OfType<(string Repo, string Path)>())
        {
            if (_paths.Has(touched.Repo, touched.Path))
            {
                _ = targets.Add(touched);
            }
            else
            {
                _removedTouches++;
            }
        }
    }

    // A record's file edges, from everything it touched, cited or defined: past TouchCap (counted by (repo, path), so one relative path in two repos is two) it gets none, is counted, and is handed on to roll up to features once the homes are known. A record under the cap keeps its file edges and is handed on too, to roll up beside them.
    private int Emit(RecordKey evidence, GraphNode node, HashSet<(string Repo, string Path)> targets)
    {
        var rolled = new RolledRecord(node, evidence, [.. targets.Select(t => t.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
        if (targets.Count > MapRecordSources.TouchCap)
        {
            _overCap++;
            _overCapPaths += targets.Count;
            _overflow.Add(rolled);
            return 0;
        }

        _underCap.AddRange(targets.Count > 0 ? [rolled] : []);
        return targets.OrderBy(t => t.Path, StringComparer.Ordinal).ThenBy(t => t.Repo, StringComparer.Ordinal).Count(t => AddCode(node.Key, t.Path, evidence));
    }

    private bool AddCode(NodeKey from, string path, RecordKey evidence) =>
        NodeKey.TryFrom("code:" + path, out var code) && AddEdge(new GraphEdge(from, code, EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(evidence)]));

    // One edge per (from, to): a second link kind to the same record, or a path cited twice, adds its evidence to that edge. True when the edge is new.
    private bool AddEdge(GraphEdge edge)
    {
        var key = (edge.From.Value, edge.To.Value);
        if (!_edges.TryGetValue(key, out var kept))
        {
            _edges[key] = edge;
            return true;
        }

        var more = edge.EvidenceOrEmpty.Where(e => !kept.EvidenceOrEmpty.Contains(e)).ToList();
        if (more.Count > 0)
        {
            _edges[key] = kept with { Evidence = [.. kept.EvidenceOrEmpty, .. more] };
        }

        return false;
    }

    private static GraphNode Node(StoredRecord record, NodeKey key) => new(key, record.Title, Provenance.Deterministic, At: record.UpdatedAt);

    // Every line of the build report's record section; only the docs older than HEAD, unresolved citations and records over the file cap also go in the second list, which is what the report calls degraded. Docs not yet synced, removed files and zero counts are notes.
    private (List<string> All, List<string> Degraded) Notes(long stamp)
    {
        var all = new List<string> { Line($"records: as of stamp {stamp}") };
        var degraded = new List<string>();
        AddIf(all, degraded, _notSynced, n => Line($"records: {n} docs not yet synced"), stale: false);
        AddIf(all, degraded, _older, n => Line($"records: {n} docs older than HEAD"), stale: true);
        var resolved = _cited.GetValueOrDefault(CitationOutcome.Resolved);
        var missing = _cited.GetValueOrDefault(CitationOutcome.Missing);
        var ambiguous = _cited.GetValueOrDefault(CitationOutcome.Ambiguous);
        AddIf(all, degraded, resolved + missing + ambiguous, _ => Line($"citations: {resolved} resolved, {missing + ambiguous} unresolved ({missing} missing, {ambiguous} ambiguous)"), stale: missing + ambiguous > 0);
        AddIf(all, degraded, _removedTouches, n => Line($"touched files since removed: {n}"), stale: false);
        AddIf(all, degraded, _overCap, n => Line($"records over the file cap: {n} ({_overCapPaths} paths)"), stale: true);
        return (all, degraded);
    }

    private static void AddIf(List<string> all, List<string> degraded, int count, Func<int, string> line, bool stale)
    {
        if (count > 0)
        {
            all.Add(line(count));
            if (stale)
            {
                degraded.Add(all[^1]);
            }
        }
    }

    private static string Line(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
