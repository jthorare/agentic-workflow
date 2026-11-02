using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Transcripts;

[Lane(TestLane.Unit)]
public sealed class InMemoryTranscriptReaderContractTests : TranscriptReaderContract
{
    private const string Path = "/projects/p/sess-1.jsonl";

    protected override ValueTask<(ITranscriptReader Reader, string Path)> CreateWithStandard(CancellationToken cancellationToken) =>
        ValueTask.FromResult<(ITranscriptReader, string)>((new InMemoryTranscriptReader().Add(StandardSession, Path, Standard()), Path));

    protected override ValueTask<ITranscriptReader> CreateEmpty(CancellationToken cancellationToken) =>
        ValueTask.FromResult<ITranscriptReader>(new InMemoryTranscriptReader());
}
