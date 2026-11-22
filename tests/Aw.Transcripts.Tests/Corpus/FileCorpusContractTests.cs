using Aw.Kernel.Testing;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Testing;
using Aw.Transcripts.Tests.Fixtures;

namespace Aw.Transcripts.Tests.Corpus;

[Lane(TestLane.Integration)]
public sealed class FileCorpusContractTests : TranscriptCorpusContract, IDisposable
{
    private readonly TempRoots _roots = new();

    protected override ValueTask<(ITranscriptCorpus Corpus, Func<string, SessionRef> RefOf)> CreateStandard(CancellationToken cancellationToken)
    {
        var refOf = StandardFiles.Write(_roots);
        return ValueTask.FromResult<(ITranscriptCorpus, Func<string, SessionRef>)>((_roots.Corpus(), refOf));
    }

    protected override ValueTask<ITranscriptCorpus> CreateEmpty(CancellationToken cancellationToken) =>
        ValueTask.FromResult<ITranscriptCorpus>(_roots.Corpus());

    public void Dispose() => _roots.Dispose();
}
