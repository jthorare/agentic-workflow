using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IGraphTranscriptPort"/> over seeded sessions, held to <see cref="GraphTranscriptPortContract"/>.</summary>
/// <param name="sessions">The sessions.</param>
public sealed class FakeGraphTranscripts(IReadOnlyList<TranscriptSeed> sessions) : IGraphTranscriptPort
{
    /// <summary>How many times <see cref="Read"/> was called.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<HumanTurnRef>>> HumanTurns(TranscriptTurnQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<HumanTurnRef> found =
        [
            .. sessions
                .Where(s => !s.IsSubagent && (query.Providers.Count == 0 || query.Providers.Contains(s.Provider, StringComparer.Ordinal)) && (query.Repo is null || string.Equals(s.Repo, query.Repo, StringComparison.Ordinal)))
                .SelectMany(static s => s.Turns.Where(static t => t.Human).Select(t => new HumanTurnRef(RefOf(s, t), s.Session, t.At)))
                .Where(t => t.At >= query.Since)
                .OrderByDescending(static t => t.At),
        ];
        return ValueTask.FromResult(Results.Success(found));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<TranscriptTurnText>>> Read(IReadOnlyList<string> refs, bool withBefore, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refs);
        Reads++;
        var found = new List<TranscriptTurnText>();
        foreach (var reference in refs)
        {
            foreach (var session in sessions)
            {
                var index = session.Turns.ToList().FindIndex(t => t.Human && string.Equals(RefOf(session, t), reference, StringComparison.Ordinal));
                if (index >= 0)
                {
                    var before = withBefore && index > 0 && !session.Turns[index - 1].Human ? session.Turns[index - 1].Text : null;
                    found.Add(new TranscriptTurnText(reference, session.Turns[index].Text, before));
                    break;
                }
            }
        }

        return ValueTask.FromResult(Results.Success<IReadOnlyList<TranscriptTurnText>>(found));
    }

    private static string RefOf(TranscriptSeed session, TranscriptSeedTurn turn) => session.Session + "#" + turn.Line.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
