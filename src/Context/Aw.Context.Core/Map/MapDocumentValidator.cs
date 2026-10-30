using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// Checks a map file and turns it into a <see cref="MapImport"/> (spec Phase 3), all or nothing: SND-CONTEXT-031 for a
/// shape problem (schema version, an empty entry, an unknown surface kind, a duplicate key or alias, a disallowed or
/// self edge, an authored 'quotes' edge), SND-CONTEXT-032 for a reference to a node the file doesn't define, each with
/// its key path. Every key (node, edge end, alias, override target) passes through EntryKey, or through the key
/// migrations when the file was written with an older key version. Overrides may name nodes the file lacks: they are
/// kept, as orphaned overrides are (spec §6.1).
/// </summary>
public static class MapDocumentValidator
{
    /// <summary>The file format versions this build reads: 2 (a map holding a 'tags' edge or a roll-up count) and 1 (every other map). A build that reads only 1 refuses a 2 file with SND-CONTEXT-031.</summary>
    public static IReadOnlyList<int> SchemaVersions { get; } = [1, 2, 3];

    /// <summary>Checks <paramref name="document"/>.</summary>
    /// <param name="document">The parsed file.</param>
    /// <param name="migrations">The key migrations (KeyMigrations.Shipped in production).</param>
    /// <param name="keyVersion">The key version to bring keys to (EntryKey.Version in production).</param>
    /// <returns>The import, or SND-CONTEXT-031/032.</returns>
    public static Result<MapImport> Check(MapDocument document, IReadOnlyList<IKeyMigration> migrations, int keyVersion = EntryKey.Version)
    {
        ArgumentNullException.ThrowIfNull(document);
        var problems = new MapProblems();
        Header(document, keyVersion, problems);
        Entries(document, problems);
        if (problems.Any)
        {
            return problems.ToError();
        }

        var current = document.KeyVersion == keyVersion;
        var graph = Keys(document, migrations, keyVersion, problems);
        var (withDecisions, quotes) = WithDecisions(graph, problems);
        var edges = MapGraphChecks.Run(withDecisions, problems);
        var overrides = current ? [.. (document.Overrides ?? []).Select(Normalized)] : document.Overrides ?? [];
        return problems.Any
            ? problems.ToError()
            : new MapImport(document.Repo, document.Commit, withDecisions with { Edges = [.. edges, .. quotes] }, overrides);
    }

    private static void Header(MapDocument document, int keyVersion, MapProblems problems)
    {
        if (!SchemaVersions.Contains(document.SchemaVersion))
        {
            problems.Invalid("schemaVersion", Fmt($"{document.SchemaVersion} isn't supported (this build reads {string.Join(", ", SchemaVersions)})"));
        }

        if (document.MapVersion is <= 0)
        {
            problems.Invalid("mapVersion", Fmt($"{document.MapVersion} isn't a map version (1 or more)"));
        }

        if (document.Commit is { } commit && !MapHead.IsCommit(commit))
        {
            problems.Invalid("commit", "is not a commit: 7 to 40 lowercase hex characters");
        }

        if (document.KeyVersion < 1 || document.KeyVersion > keyVersion)
        {
            problems.Invalid("keyVersion", Fmt($"{document.KeyVersion} isn't supported (1 to {keyVersion})"));
        }
    }

    /// <summary>A list may hold a null entry ('- ' with nothing after it in YAML); each is reported, not dereferenced.</summary>
    private static void Entries(MapDocument document, MapProblems problems)
    {
        Each("nodes", document.Nodes, problems);
        Each("edges", document.Edges, problems);
        Each("aliases", document.Aliases, problems);
        Each("decisions", document.Decisions, problems);
        Each("overrides", document.Overrides, problems);
    }

    private static void Each<T>(string name, IReadOnlyList<T?>? items, MapProblems problems)
        where T : class
    {
        for (var i = 0; i < (items?.Count ?? 0); i++)
        {
            if (items![i] is null)   // why: the loop runs only when items is not null
            {
                problems.Invalid(Fmt($"{name}[{i}]"), "an entry is required");
            }
        }
    }

    private static GraphSnapshot Keys(MapDocument document, IReadOnlyList<IKeyMigration> migrations, int keyVersion, MapProblems problems)
    {
        var graph = new GraphSnapshot(document.Nodes, document.Edges ?? [], document.Aliases ?? [], document.Decisions ?? []);
        if (document.KeyVersion == keyVersion)
        {
            return new GraphSnapshot(
                [.. graph.Nodes.Select(n => n with { Key = EntryKey.For(n.Key) })],
                [.. graph.Edges.Select(e => e with { From = EntryKey.For(e.From), To = EntryKey.For(e.To) })],
                [.. graph.Aliases.Select(a => new NodeAlias(EntryKey.For(a.Alias), EntryKey.For(a.Target)))],
                graph.Decisions);
        }

        var (migrated, reached) = KeyMigrations.Apply(graph, document.KeyVersion, migrations);
        if (reached != keyVersion)
        {
            problems.Invalid("keyVersion", Fmt($"keys of version {document.KeyVersion} can't be migrated to {keyVersion}"));
        }

        return migrated;
    }

    /// <summary>Adds each decision's node, its record node (when the file lacks it) and its implied 'quotes' edge, kept apart from the file's edges.</summary>
    private static (GraphSnapshot Graph, List<GraphEdge> Quotes) WithDecisions(GraphSnapshot graph, MapProblems problems)
    {
        var nodes = graph.Nodes.ToList();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Key.Kind == NodeKind.Decision)
            {
                problems.Invalid(Fmt($"nodes[{i}].key"), "decisions go under 'decisions:'; their nodes are implied");
            }
        }

        var keys = nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var quotes = new List<GraphEdge>();
        for (var i = 0; i < graph.Decisions.Count; i++)
        {
            var d = graph.Decisions[i];
            if (Refused(d, i, keys, problems) || !NodeKey.TryFrom($"record:{d.Record.Value}", out var record))
            {
                continue;
            }

            nodes.Add(new GraphNode(d.ToKey(), d.Wording, d.Provenance, Evidence: [new EvidenceRef(d.Record)], At: d.At));
            if (keys.Add(record.Value))
            {
                nodes.Add(new GraphNode(record, d.Record.Value, Provenance.Deterministic, At: d.At));
            }

            quotes.Add(new GraphEdge(d.ToKey(), record, EdgeType.Quotes, Provenance.Deterministic, At: d.At));
        }

        return (graph with { Nodes = nodes }, quotes);
    }

    /// <summary>A decision's own problems, reported at its key path: blank wording, a duplicate, a record key too long for a node key.</summary>
    private static bool Refused(DecisionRow d, int i, HashSet<string> keys, MapProblems problems)
    {
        var problem = string.IsNullOrWhiteSpace(d.Wording) || string.IsNullOrWhiteSpace(d.Quote) ? ("wording", "a quote and a wording are required")
            : !keys.Add(d.ToKey().Value) ? ("quote", "the same record and quote are decided twice")
            : !NodeKey.TryFrom($"record:{d.Record.Value}", out _) ? ("record", "too long to be pointed at by a record node (at most 1017 characters)")
            : default((string, string)?);
        if (problem is not { } p)
        {
            return false;
        }

        problems.Invalid(Fmt($"decisions[{i}].{p.Item1}"), p.Item2);
        return true;
    }

    private static MapOverride Normalized(MapOverride o) =>
        o with { Target = EntryKey.For(o.Target), Other = o.Other is { } other ? EntryKey.For(other) : null };

    private static string Fmt(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
