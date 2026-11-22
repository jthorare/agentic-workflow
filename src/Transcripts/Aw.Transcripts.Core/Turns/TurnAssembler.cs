using Aw.Transcripts.Core.Refs;

namespace Aw.Transcripts.Core.Turns;

/// <summary>
/// Folds one file's entries into turns: each human entry is a turn, and the assistant entries after it, up to the next
/// human entry or machine prompt, are one assistant turn (text blocks joined by newlines, tool names once each).
/// </summary>
public static class TurnAssembler
{
    /// <summary>The turns of one file, in order.</summary>
    /// <param name="session">The file's session ref.</param>
    /// <param name="entries">The file's entries, in line order.</param>
    /// <returns>The turns. An assistant run with no text and no tool call is no turn.</returns>
    public static IReadOnlyList<TranscriptTurn> Assemble(SessionRef session, IEnumerable<TranscriptEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        List<TranscriptTurn> turns = [];
        Run? run = null;
        foreach (var entry in entries)
        {
            if (entry.Kind == EntryKind.Assistant)
            {
                run ??= new Run(entry);
                run.Add(entry);
                continue;
            }

            Flush(session, turns, ref run);
            if (entry.Kind == EntryKind.Human)
            {
                turns.Add(new TranscriptTurn(TranscriptRefs.ForTurn(session, entry.Line), TurnRole.Human, entry.At, entry.Text, []));
            }
        }

        Flush(session, turns, ref run);
        return turns;
    }

    private static void Flush(SessionRef session, List<TranscriptTurn> turns, ref Run? run)
    {
        if (run is { } open && (open.Texts.Count > 0 || open.Tools.Count > 0))
        {
            turns.Add(new TranscriptTurn(TranscriptRefs.ForTurn(session, open.First.Line), TurnRole.Assistant, open.First.At, string.Join('\n', open.Texts), [.. open.Tools]));
        }

        run = null;
    }

    private sealed class Run(TranscriptEntry first)
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public TranscriptEntry First { get; } = first;

        public List<string> Texts { get; } = [];

        public List<string> Tools { get; } = [];

        public void Add(TranscriptEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.Text))
            {
                Texts.Add(entry.Text);
            }

            foreach (var tool in entry.Tools)
            {
                if (_seen.Add(tool))
                {
                    Tools.Add(tool);
                }
            }
        }
    }
}
