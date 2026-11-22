using Aw.Kernel.Testing;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Testing;

namespace Aw.Transcripts.Tests.Corpus;

[Lane(TestLane.Unit)]
public sealed class InMemoryCorpusContractTests : TranscriptCorpusContract
{
    private static SessionRef RefOf(string name) => TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, $"fake/{name}.jsonl");

    protected override ValueTask<(ITranscriptCorpus Corpus, Func<string, SessionRef> RefOf)> CreateStandard(CancellationToken cancellationToken) =>
        ValueTask.FromResult<(ITranscriptCorpus, Func<string, SessionRef>)>((new InMemoryTranscriptCorpus(StandardCorpus.Transcripts(RefOf)), RefOf));

    protected override ValueTask<ITranscriptCorpus> CreateEmpty(CancellationToken cancellationToken) =>
        ValueTask.FromResult<ITranscriptCorpus>(new InMemoryTranscriptCorpus([]));
}
