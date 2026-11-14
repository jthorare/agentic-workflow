using Aw.CodeIndex.Core.Embedding;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Features;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Features.EmbedCodeIndex;
using Aw.CodeIndex.Infrastructure.Parsing;
using Aw.CodeIndex.Infrastructure.Scrubbing;
using Aw.CodeIndex.Infrastructure.Snapshot;
using Aw.CodeIndex.Infrastructure.Store;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Tests.Build;

/// <summary>The build and the facade over the real adapters (git, Roslyn, SQLite files in a temp directory); only the heavy slot and the main-branch check are fakes, since no real ones belong to this package.</summary>
internal sealed class RealIndex : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aw-codeindex-" + RealTime.NewGuid().ToString("N"));

    public RealIndex(IEmbedder? embedder = null, IGraphRunner? graph = null)
    {
        Embedder = embedder ?? new FakeEmbedder();
        _ = Directory.CreateDirectory(_directory);
        var snapshot = new GitRepoSnapshot();
        Store = new SqliteIndexStore(Path.Combine(_directory, "index"), TimeProvider.System, new InProcessRepoIndexLock());
        var layers = new BuildLayers(new EmbeddingsLayer(Embedder, TimeProvider.System), new GraphLayer(snapshot, TimeProvider.System, new RedactingBodyScrubber(), graph));
        Service = new BuildCodeIndexService(new FakeRepoHeads(), snapshot, new StructureWriter(snapshot, [new CSharpParser()], new FileParseQuarantine(Path.Combine(_directory, "index")), new RedactingBodyScrubber()), layers, Store, TimeProvider.System);
        Facade = new CodeIndexFacade(Service, Store, Embedder, graph);
        Embed = new EmbedCodeIndexService(Store, layers.Embeddings, TimeProvider.System);
    }

    /// <summary>The directory that holds the repo index files (the .db files and the .current pointers).</summary>
    public string IndexDirectory => Path.Combine(_directory, "index");

    public IEmbedder Embedder { get; }

    public IIndexStore Store { get; }


    public BuildCodeIndexService Service { get; }

    public CodeIndexFacade Facade { get; }

    public IService<EmbedCodeIndexRequest, EmbedCodeIndexResponse> Embed { get; }

    /// <summary>Counts the rows of one graph table ('graph_nodes' or 'graph_edges') in the repo's published index file, read straight from SQLite (the graph tables have no read on the facade).</summary>
    public async Task<long> CountRows(RepoName repo, string table)
    {
        var directory = Path.Combine(_directory, "index");
        var live = Path.Combine(directory, $"{repo.Value}.{(await File.ReadAllTextAsync(Path.Combine(directory, $"{repo.Value}.current"), CancellationToken.None)).Trim()}.db");
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = live, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await using (connection)
        {
            await connection.OpenAsync(CancellationToken.None);
            var command = connection.CreateCommand();
            await using (command)
            {
                command.CommandText = table switch
                {
                    "graph_nodes" => "SELECT COUNT(*) FROM graph_nodes",
                    "graph_edges" => "SELECT COUNT(*) FROM graph_edges",
                    _ => throw new ArgumentOutOfRangeException(nameof(table), table, "not a graph table"),
                };
                return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
            }
        }
    }

    public static CodeIndexScope ScopeOf(CodeIndexRepo repo) => new([repo], new CodeIndexCaller(CallerKind.Service, Repo: null));

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
