using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract.DataStores;
using Aw.Context.Core.Extract.Imports;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Extract;

/// <summary>
/// Finds the data stores a repo declares and the code files that read or write them (S-4's deterministic anchors for things). A schema file
/// (one of the schema globs) declares a store; a thing node 'thing:&lt;kebab-name&gt;' rests on the files that declare it. A source file that
/// reads or writes a declared store gets a code node and a 'reads' or 'writes' edge to the thing, resting on that file. Pure and ordered.
/// </summary>
public static class DataStorePass
{
    private const int MaxSchemaLength = 512_000;

    /// <summary>Runs the adapters over the files.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="files">The files at the commit.</param>
    /// <param name="adapters">The adapters.</param>
    /// <param name="schemaGlobs">The globs of the files that declare stores.</param>
    /// <returns>The things, the code files that use them and the edges, ordered.</returns>
    public static ExtractedMap Run(ContextRepoName repo, IReadOnlyList<RepoFile> files, IReadOnlyList<IDataStoreAdapter> adapters, IReadOnlyList<string> schemaGlobs)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(schemaGlobs);
        var ordered = files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        var schema = ordered.Where(f => f.Text.Length <= MaxSchemaLength && PathDenylist.Matches(f.Path, schemaGlobs)).ToList();
        var stores = Declare(repo, schema, adapters);
        if (stores.Count == 0)
        {
            return ExtractedMap.Empty;
        }

        var names = stores.Values.SelectMany(s => s.Names).ToHashSet(StringComparer.Ordinal);
        var schemaPaths = schema.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var byName = stores.Values.SelectMany(s => s.Names.Select(n => (Name: n, s.Key))).DistinctBy(p => p.Name, StringComparer.Ordinal).ToDictionary(p => p.Name, p => p.Key, StringComparer.Ordinal);
        List<GraphNode> nodes = [.. stores.Values.Select(s => new GraphNode(s.Key, s.Label, Provenance.Deterministic, 1.0, s.Evidence))];
        List<GraphEdge> edges = [];
        var access = new Access(adapters, names, byName, Bindings(schema, adapters), ordered);
        foreach (var file in ordered.Where(f => !schemaPaths.Contains(f.Path) && Recognizers.Source(f)))
        {
            var uses = access.Of(file);
            if (uses.Count > 0 && MapExtractor.CodeKey(file.Path) is { } code && MapExtractor.Evidence(repo, file.Path) is { } evidence)
            {
                nodes.Add(new GraphNode(code, file.Path[(file.Path.LastIndexOf('/') + 1)..], Provenance.Deterministic));
                edges.AddRange(uses.Select(u => new GraphEdge(code, u.Key, u.Access == DataAccess.Read ? EdgeType.Reads : EdgeType.Writes, Provenance.Deterministic, 1.0, [evidence])));
            }
        }

        return ExtractedMap.Empty.Plus(new ExtractedMap(nodes, edges));
    }

    // The identifier each schema file declares for a store, by file: what the code's imports are followed to.
    private static Dictionary<(string File, string Identifier), string> Bindings(List<RepoFile> schema, IReadOnlyList<IDataStoreAdapter> adapters)
    {
        var bindings = new Dictionary<(string, string), string>();
        foreach (var file in schema)
        {
            foreach (var binding in adapters.OfType<IBindingDataStoreAdapter>().SelectMany(a => a.DeclaredBindings(file.Text)))
            {
                _ = bindings.TryAdd((file.Path, binding.Identifier), binding.Store.ToLowerInvariant());
            }
        }

        return bindings;
    }

    // The uses of declared stores in one file, by the adapters: an adapter that follows imports gets the file's scope, the others the declared names.
    private sealed class Access(IReadOnlyList<IDataStoreAdapter> adapters, HashSet<string> names, Dictionary<string, NodeKey> byName, Dictionary<(string File, string Identifier), string> bindings, List<RepoFile> files)
    {
        private readonly TsSymbolResolver? _resolver = bindings.Count == 0 ? null : new TsSymbolResolver([.. files.Where(f => !RepoPath.InDependencyFolder(f.Path)).Select(static f => new SourceFile(f.Path, f.Text))]);

        internal List<(NodeKey Key, DataAccess Access)> Of(RepoFile file)
        {
            var scope = _resolver is null ? null : new FileScope(file.Path, _resolver, bindings);
            return [.. adapters.SelectMany(a => scope is not null && a is IBindingDataStoreAdapter binding ? binding.Accesses(file.Text, scope) : a.Accesses(file.Text, names))
                .Where(a => byName.ContainsKey(a.Store.ToLowerInvariant()))
                .Select(a => (Key: byName[a.Store.ToLowerInvariant()], a.Access)).Distinct()];
        }
    }

    private sealed record Store(NodeKey Key, string Label, HashSet<string> Names, List<EvidenceRef> Evidence);

    // One store per thing key, in the order of the sorted schema files; names that differ only by case, '_' or '.' share a key.
    private static Dictionary<string, Store> Declare(ContextRepoName repo, List<RepoFile> schema, IReadOnlyList<IDataStoreAdapter> adapters)
    {
        var stores = new Dictionary<string, Store>(StringComparer.Ordinal);
        foreach (var file in schema)
        {
            foreach (var name in adapters.SelectMany(a => a.Declared(file.Text)).Distinct(StringComparer.Ordinal))
            {
                if (ThingKey(name) is not { } key || MapExtractor.Evidence(repo, file.Path) is not { } evidence)
                {
                    continue;
                }

                if (!stores.TryGetValue(key.Value, out var store))
                {
                    store = new Store(key, name, new HashSet<string>(StringComparer.Ordinal), []);
                    stores[key.Value] = store;
                }

                _ = store.Names.Add(name.ToLowerInvariant());
                if (!store.Evidence.Contains(evidence))
                {
                    store.Evidence.Add(evidence);
                }
            }
        }

        return stores;
    }

    internal static NodeKey? ThingKey(string name)
    {
        var kebab = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            _ = kebab.Append(char.IsLetterOrDigit(c) ? c : '-');
        }

        var text = kebab.ToString().Trim('-');
        return text.Length > 0 && NodeKey.TryFrom("thing:" + text, out var key) ? EntryKey.For(key) : null;
    }
}
