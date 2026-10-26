using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Transcripts;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Transcripts;

[Lane(TestLane.Integration)]
public sealed class ClaudeTranscriptReaderContractTests : TranscriptReaderContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override ValueTask<(ITranscriptReader Reader, string Path)> CreateWithStandard(CancellationToken cancellationToken)
    {
        var path = _state.Standard("my-project");
        return ValueTask.FromResult<(ITranscriptReader, string)>((new ClaudeTranscriptReader(Path.Combine(_state.Path, "projects")), path));
    }

    protected override ValueTask<ITranscriptReader> CreateEmpty(CancellationToken cancellationToken) =>
        ValueTask.FromResult<ITranscriptReader>(new ClaudeTranscriptReader(Path.Combine(_state.Path, "no-such-projects")));

    public void Dispose() => _state.Dispose();
}
