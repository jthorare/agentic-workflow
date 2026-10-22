using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Features;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Infrastructure.Parsing;
using Aw.CodeIndex.Infrastructure.Scrubbing;
using Aw.CodeIndex.Infrastructure.Snapshot;
using Aw.CodeIndex.Infrastructure.Store;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Build;

/// <summary>The build and the facade over the real adapters (git, Roslyn, SQLite files in a temp directory); only the heavy slot and the main-branch check are fakes, since no real ones belong to this package.</summary>
internal sealed class RealIndex : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aw-codeindex-" + RealTime.NewGuid().ToString("N"));

    public RealIndex(FakeHeavyJobLock? heavy = null, IEmbedder? embedder = null, IGraphRunner? graph = null)
    {
        Embedder = embedder ?? new FakeEmbedder();
        _ = Directory.CreateDirectory(_directory);
        var snapshot = new GitRepoSnapshot();
        Store = new SqliteIndexStore(Path.Combine(_directory, "index"), TimeProvider.System);
        Heavy = heavy ?? new FakeHeavyJobLock();
        Service = new BuildCodeIndexService(new FakeRepoHeads(), snapshot, new StructureWriter(snapshot, [new CSharpParser()], new FileParseQuarantine(Path.Combine(_directory, "index")), new RedactingBodyScrubber()), new BuildLayers(new EmbeddingsLayer(Embedder, TimeProvider.System), new GraphLayer(snapshot, TimeProvider.System, graph)), Store, Heavy, TimeProvider.System);
        Facade = new CodeIndexFacade(Service, Store, Embedder, graph);
    }

    public IEmbedder Embedder { get; }

    public IIndexStore Store { get; }

    public FakeHeavyJobLock Heavy { get; }

    public BuildCodeIndexService Service { get; }

    public CodeIndexFacade Facade { get; }

    public static CodeIndexScope ScopeOf(CodeIndexRepo repo) => new([repo], new CodeIndexCaller(CallerKind.Service, Repo: null));

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
