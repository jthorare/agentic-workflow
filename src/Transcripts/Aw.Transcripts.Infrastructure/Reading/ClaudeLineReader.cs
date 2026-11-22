using Aw.Transcripts.Core.Prompts;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Infrastructure.Wire;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>
/// Claude Code lines. A user line is a human prompt only when it carries no tool result, isn't flagged meta, sidechain,
/// compaction summary or transcript-only, came from a person (origin kind 'human', or no origin in older versions), ran
/// interactively (entrypoint not 'sdk-*') and its text passes <see cref="PromptText.IsTyped"/>. Every other user line
/// with text is a machine prompt. A subagent file has no human prompt: its first user line is the parent's task.
/// </summary>
/// <param name="subagent">Whether the file is a subagent's.</param>
/// <param name="clock">The file's write time, for a line with no timestamp before any line had one.</param>
/// <param name="scrub">The secret scrubber.</param>
internal sealed class ClaudeLineReader(bool subagent, DateTimeOffset clock, Func<string, string> scrub) : ILineReader
{
    private DateTimeOffset _now = clock;

    public void Read(int number, string line, ParsedFile file)
    {
        if (WireJson.Read<ClaudeLine>(line) is not { } row)
        {
            file.SkippedLines++;
            return;
        }

        if (WireTime.Parse(row.Timestamp) is { } at)
        {
            _now = at;
            file.Saw(at);
        }

        if (file.Cwd is null && row.Cwd is { Length: > 0 } cwd)
        {
            file.Cwd = cwd;
        }

        // A line with no type, or a user or assistant line whose content is no string or block array, is of an unknown shape.
        if (row.Type is null || (Spoken(row.Type) && !ContentBlocks.IsReadable(row.Message?.Content)))
        {
            file.SkippedLines++;
            return;
        }

        var entry = row.Type switch
        {
            "user" => User(number, row),
            "assistant" => Assistant(number, row),
            _ => null,
        };
        if (entry is not null)
        {
            file.Entries.Add(entry);
        }
    }

    private TranscriptEntry? User(int number, ClaudeLine row)
    {
        var content = ContentBlocks.Read(row.Message?.Content);
        var text = content.Text;
        if (content.HasToolResult || text.Trim().Length == 0)
        {
            return null;
        }

        return IsHuman(row, text)
            ? new TranscriptEntry(number, _now, EntryKind.Human, scrub(text.Trim()), [])
            : new TranscriptEntry(number, _now, EntryKind.MachinePrompt, "", []);
    }

    private bool IsHuman(ClaudeLine row, string text) => !subagent && !Flagged(row) && FromAPerson(row) && PromptText.IsTyped(text);

    // The flags the harness sets on lines it writes itself.
    private static bool Flagged(ClaudeLine row) =>
        row.IsSidechain == true || row.IsMeta == true || row.IsCompactSummary == true || row.IsVisibleInTranscriptOnly == true;

    // Origin kind 'human' (or none, in older versions) in an interactive session: 'sdk-*' entrypoints run scripted prompts.
    private static bool FromAPerson(ClaudeLine row) =>
        (row.Origin?.Kind is null || string.Equals(row.Origin.Kind, "human", StringComparison.Ordinal))
        && row.Entrypoint?.StartsWith("sdk", StringComparison.Ordinal) != true;

    private TranscriptEntry? Assistant(int number, ClaudeLine row)
    {
        // A sidechain line in a main file is an old-format subagent's; a synthetic message is the harness's.
        if ((!subagent && row.IsSidechain == true) || string.Equals(row.Message?.Model, "<synthetic>", StringComparison.Ordinal))
        {
            return null;
        }

        var content = ContentBlocks.Read(row.Message?.Content);
        var text = content.Text.Trim();
        return text.Length == 0 && content.Tools.Count == 0
            ? null
            : new TranscriptEntry(number, _now, EntryKind.Assistant, text.Length == 0 ? "" : scrub(text), content.Tools);
    }

    private static bool Spoken(string type) => string.Equals(type, "user", StringComparison.Ordinal) || string.Equals(type, "assistant", StringComparison.Ordinal);
}
