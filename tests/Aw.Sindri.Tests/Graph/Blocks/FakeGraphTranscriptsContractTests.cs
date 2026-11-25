using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>Binds <see cref="FakeGraphTranscripts"/> to <see cref="GraphTranscriptPortContract"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeGraphTranscriptsContractTests : GraphTranscriptPortContract
{
    protected override ValueTask<IGraphTranscriptPort> Create(IReadOnlyList<TranscriptSeed> sessions) => ValueTask.FromResult<IGraphTranscriptPort>(new FakeGraphTranscripts(sessions));
}
