using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Core.Turns;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The transcript blocks' port over the local transcript corpus (issue 3787, piece 2b). Sindri Core can't reference Aw.Transcripts, and the Host already does
/// (for 'sindri transcripts'), so the adapter lives here, like <see cref="GraphModelPortAdapter"/>. It only reads.
/// </summary>
/// <param name="corpus">The transcript corpus.</param>
internal sealed class CorpusTranscriptPort(ITranscriptCorpus corpus) : IGraphTranscriptPort
{
    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<HumanTurnRef>>> HumanTurns(TranscriptTurnQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var providers = new HashSet<TranscriptProvider>();
        foreach (var name in query.Providers)
        {
            if (TranscriptProviderNames.TryParse(name, out var provider))
            {
                _ = providers.Add(provider);
            }
        }

        // why: the corpus filters one provider itself; several are filtered here, and a run that names only unknown ones gets none
        var listed = await corpus.ListSessions(new SessionQuery(query.Since) { IncludeSubagents = false, Repo = query.Repo, Provider = providers.Count == 1 ? providers.Single() : null }, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var turns = new List<HumanTurnRef>();
        foreach (var session in listed.Value.Where(s => query.Providers.Count == 0 || providers.Contains(s.Provider)))
        {
            var read = await corpus.ReadSession(session.Ref, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                continue;   // why: a file that went away or can't be read between the list and the read is skipped, not the whole list
            }

            turns.AddRange(read.Value.Turns.Where(t => t.Role == TurnRole.Human && t.At >= query.Since).Select(t => new HumanTurnRef(t.Ref.Value, session.Ref.Value, t.At)));
        }

        return Kernel.Results.Success<IReadOnlyList<HumanTurnRef>>([.. turns.OrderByDescending(static t => t.At).ThenBy(static t => t.Ref, StringComparer.Ordinal)]);
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<TranscriptTurnText>>> Read(IReadOnlyList<string> refs, bool withBefore, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var sessions = new Dictionary<string, SessionTranscript?>(StringComparer.Ordinal);
        var found = new List<TranscriptTurnText>();
        foreach (var reference in refs)
        {
            if (!TurnRef.TryFrom(reference, out var turn))
            {
                continue;   // why: a ref of the wrong shape names no turn
            }

            var session = turn.SessionOf();
            if (!sessions.TryGetValue(session.Value, out var transcript))
            {
                var read = await corpus.ReadSession(session, cancellationToken).ConfigureAwait(false);
                transcript = read.IsSuccess ? read.Value : null;   // why: a session no file has any more (or can't be read) leaves its refs out
                sessions[session.Value] = transcript;
            }

            if (transcript is not null && Text(transcript, turn, withBefore) is { } text)
            {
                found.Add(text);
            }
        }

        return Kernel.Results.Success<IReadOnlyList<TranscriptTurnText>>(found);
    }

    private static TranscriptTurnText? Text(SessionTranscript transcript, TurnRef turn, bool withBefore)
    {
        var index = -1;
        for (var i = 0; i < transcript.Turns.Count; i++)
        {
            if (transcript.Turns[i].Ref == turn && transcript.Turns[i].Role == TurnRole.Human)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return null;
        }

        var before = withBefore && index > 0 && transcript.Turns[index - 1].Role == TurnRole.Assistant ? transcript.Turns[index - 1].Text : null;
        return new TranscriptTurnText(turn.Value, transcript.Turns[index].Text, before);
    }
}
