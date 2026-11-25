using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Testing.Workflow;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Testing;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>Binds <see cref="CorpusTranscriptPort"/> to <see cref="GraphTranscriptPortContract"/> over the in-memory transcript corpus, seeded from the same rows the fake takes.</summary>
[Lane(TestLane.Unit)]
public sealed class CorpusTranscriptPortContractTests : GraphTranscriptPortContract
{
    protected override ValueTask<IGraphTranscriptPort> Create(IReadOnlyList<TranscriptSeed> sessions) =>
        ValueTask.FromResult<IGraphTranscriptPort>(new CorpusTranscriptPort(new InMemoryTranscriptCorpus(sessions.Select(Transcript))));

    private static SessionTranscript Transcript(TranscriptSeed seed)
    {
        var session = SessionRef.From(seed.Session);
        var provider = TranscriptProviderNames.TryParse(seed.Provider, out var parsed) ? parsed : throw new ArgumentException($"no provider '{seed.Provider}'", nameof(seed));
        var turns = seed.Turns.Select(t => new TranscriptTurn(TranscriptRefs.ForTurn(session, t.Line), t.Human ? TurnRole.Human : TurnRole.Assistant, t.At, t.Text, [])).ToList();
        var last = seed.Turns.Count == 0 ? DateTimeOffset.UnixEpoch : seed.Turns.Max(static t => t.At);
        var first = seed.Turns.Count == 0 ? DateTimeOffset.UnixEpoch : seed.Turns.Min(static t => t.At);
        return new SessionTranscript(new TranscriptSession(session, provider, first, last, seed.Repo, seed.IsSubagent, Parent: null, turns.Count(static t => t.Role == TurnRole.Human)), turns, SkippedLines: 0);
    }
}
