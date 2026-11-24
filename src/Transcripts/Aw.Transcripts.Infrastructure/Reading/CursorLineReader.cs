using Aw.Transcripts.Core.Prompts;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Infrastructure.Wire;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>
/// Cursor agent-transcript lines (built from the format notes and fixture, not from real files: see
/// docs/csharp/transcripts.md). Lines carry no timestamp and no cwd; a user line's '&lt;timestamp&gt;' block sets the clock
/// for it and the lines after it, else the file's write time is used. The prompt is the '&lt;user_query&gt;' text
/// (<see cref="CursorPrompt.Body"/>); a subagent file has no human prompt.
/// </summary>
/// <param name="subagent">Whether the file is a subagent's.</param>
/// <param name="clock">The file's write time.</param>
/// <param name="scrub">The secret scrubber.</param>
internal sealed class CursorLineReader(bool subagent, DateTimeOffset clock, Func<string, string> scrub) : ILineReader
{
    private DateTimeOffset _now = clock;

    public void Read(int number, string line, ParsedFile file)
    {
        if (WireJson.Read<CursorLine>(line) is not { } row)
        {
            file.SkippedLines++;
            return;
        }

        // A user or assistant line with no readable content, or a line with neither such a role nor a type, is of an unknown shape.
        var spoken = string.Equals(row.Role, "user", StringComparison.Ordinal) || string.Equals(row.Role, "assistant", StringComparison.Ordinal);
        if ((spoken && !ContentBlocks.IsReadable(row.Message?.Content)) || (!spoken && row.Type is null))
        {
            file.SkippedLines++;
            return;
        }

        var content = ContentBlocks.Read(row.Message?.Content);
        var entry = row.Role switch
        {
            "user" => User(number, content.Text, file),
            "assistant" => Assistant(number, content),
            _ => null,
        };
        if (entry is not null)
        {
            file.Entries.Add(entry);
        }
    }

    private TranscriptEntry? User(int number, string text, ParsedFile file)
    {
        if (CursorPrompt.Timestamp(text) is { } at)
        {
            _now = at;
            file.Saw(at);
        }

        var body = CursorPrompt.Body(text).Trim();
        if (body.Length == 0)
        {
            return null;
        }

        return !subagent && PromptText.IsTyped(body)
            ? new TranscriptEntry(number, _now, EntryKind.Human, scrub(body), [])
            : new TranscriptEntry(number, _now, EntryKind.MachinePrompt, "", []);
    }

    private TranscriptEntry? Assistant(int number, ContentBlocks content)
    {
        var text = content.Text.Trim();
        return text.Length == 0 && content.Tools.Count == 0
            ? null
            : new TranscriptEntry(number, _now, EntryKind.Assistant, text.Length == 0 ? "" : scrub(text), content.Tools);
    }
}
