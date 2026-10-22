using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Graph;

[Trait("Lane", "unit")]
public sealed class FakeGraphRunnerContractTests : GraphRunnerContract
{
    internal static GraphRequest RequestOf(IBlobReader? blobs = null)
        => new(RepoName.From("web"), CommitSha.From("1111111111111111111111111111111111111111"), [.. RequestPaths.Select(p => new SnapshotFile(p, "b-" + p, 10))], blobs ?? new NoBlobs(), new GraphSettings(Enabled: true, MaxMegabytes: 64, MaxNodes: 1_000));

    protected override (IGraphRunner Runner, GraphRequest Request) ArrangeWithOutsider()
    {
        var graph = new GraphSnapshot(
            [new GraphNode(CallerKey, "src/a.ts", "f", 1, Community: null), new GraphNode(CalleeKey, "src/b.ts", "g", 2, Community: null), new GraphNode(OutsiderKey, "src/x.ts", "h", 3, Community: null)],
            [new GraphEdge(CallerKey, CalleeKey, "calls", "EXTRACTED", 4), new GraphEdge(CallerKey, OutsiderKey, "calls", "EXTRACTED", 5)]);
        return (new FakeGraphRunner { Graph = graph }, RequestOf());
    }

    protected override (IGraphRunner Runner, GraphRequest Request) ArrangeMissingTool()
        => (new FakeGraphRunner { ProbeError = CodeIndexErrors.GraphUnavailable.With("missing"), RunError = CodeIndexErrors.GraphUnavailable.With("missing") }, RequestOf());

    internal sealed class NoBlobs : IBlobReader
    {
        public ValueTask<Kernel.Result<byte[]?>> Read(string blob, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<byte[]?>>((byte[]?)null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
