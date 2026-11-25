using System.Text.Json;
using Aw.Transcripts.Core.Prompts;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Infrastructure.Wire;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>
/// Codex rollout lines. The first line is session_meta (id, cwd, source; a spawned agent's source is
/// '{ subagent: { thread_spawn: { parent_thread_id } } }'). A human prompt is an 'event_msg' 'user_message' of an
/// interactive (not 'exec') main session whose text passes <see cref="PromptText.IsTyped"/>. Older rollouts also write a
/// 'response_item' user message per prompt (the same text, or wrapped in AGENTS.md and environment context, which
/// <see cref="PromptText.IsTyped"/> rejects); newer ones write only that. The reader takes a typed 'response_item' user
/// message as a prompt too, unless it repeats the open prompt (same text, no assistant output between), whichever record
/// comes first, so each turn counts once and a mixed-format file keeps every prompt. Assistant output is the
/// 'response_item' assistant messages and tool calls.
/// </summary>
/// <param name="clock">The file's write time, for a line with no timestamp before any line had one.</param>
/// <param name="scrub">The secret scrubber.</param>
internal sealed class CodexLineReader(DateTimeOffset clock, Func<string, string> scrub) : ILineReader
{
    private DateTimeOffset _now = clock;

    // The latest human prompt not yet followed by assistant output: its text, whether it came from an 'event_msg', and its
    // index in file.Entries. A prompt of the other record kind with the same text is its duplicate.
    private (string Text, bool FromEvent, int Index)? _open;

    public void Read(int number, string line, ParsedFile file)
    {
        if (WireJson.Read<CodexLine>(line) is not { } row)
        {
            file.SkippedLines++;
            return;
        }

        if (WireTime.Parse(row.Timestamp) is { } at)
        {
            _now = at;
            file.Saw(at);
        }

        // Every rollout line is '{ timestamp, type, payload }'; one without a type or a payload is of an unknown shape.
        if (row.Type is null || row.Payload is not { } payload)
        {
            file.SkippedLines++;
            return;
        }

        Add(Entry(number, row.Type, payload, file), file);
    }

    private void Add(TranscriptEntry? entry, ParsedFile file)
    {
        if (entry is null)
        {
            return;
        }

        if (entry.Kind == EntryKind.Assistant)
        {
            _open = null;
        }

        file.Entries.Add(entry);
    }

    private TranscriptEntry? Entry(int number, string type, CodexPayload payload, ParsedFile file) =>
        (type, payload.Type) switch
        {
            ("session_meta", _) => Meta(payload, file),
            ("event_msg", "user_message") => Prompt(number, payload, file),
            ("response_item", "message") when payload.Role == "user" => ItemPrompt(number, payload, file),
            ("response_item", "message") when payload.Role == "assistant" => Said(number, payload),
            ("response_item", "function_call" or "custom_tool_call") when payload.Name is { Length: > 0 } name => Tool(number, name),
            ("response_item", "local_shell_call") => Tool(number, "local_shell"),
            ("response_item", "web_search_call") => Tool(number, "web_search"),
            _ => null,
        };

    private static TranscriptEntry? Meta(CodexPayload payload, ParsedFile file)
    {
        file.Cwd ??= payload.Cwd is { Length: > 0 } cwd ? cwd : null;
        if (payload.Source is not { } source)
        {
            return null;
        }

        if (source.ValueKind == JsonValueKind.String)
        {
            file.NonInteractive = string.Equals(source.GetString(), "exec", StringComparison.Ordinal);
        }
        else if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty("subagent", out var subagent))
        {
            file.IsSubagent = true;
            file.ParentThread = subagent.ValueKind == JsonValueKind.Object && subagent.TryGetProperty("thread_spawn", out var spawn)
                ? ContentBlocks.Str(spawn, "parent_thread_id")
                : null;
        }

        return null;
    }

    private TranscriptEntry? Prompt(int number, CodexPayload payload, ParsedFile file)
    {
        var text = payload.Message is { ValueKind: JsonValueKind.String } message ? message.GetString() ?? "" : "";
        if (text.Trim().Length == 0)
        {
            return null;
        }

        if (file.IsSubagent || file.NonInteractive || !PromptText.IsTyped(text))
        {
            return new TranscriptEntry(number, _now, EntryKind.MachinePrompt, "", []);
        }

        var key = PromptText.WithoutIdeBlocks(text).Trim();
        var human = new TranscriptEntry(number, _now, EntryKind.Human, scrub(key), []);
        if (_open is { FromEvent: false } open && string.Equals(open.Text, key, StringComparison.Ordinal))
        {
            // The response_item copy came first: the event_msg is the record of the turn, so it takes the copy's place.
            file.Entries[open.Index] = human;
            _open = (key, true, open.Index);
            return null;
        }

        _open = (key, true, file.Entries.Count);
        return human;
    }

    // A user 'response_item': a prompt only when typed text of an interactive main session and not the copy of the open one.
    private TranscriptEntry? ItemPrompt(int number, CodexPayload payload, ParsedFile file)
    {
        var text = ContentBlocks.Read(payload.Content).Text;
        if (file.IsSubagent || file.NonInteractive || !PromptText.IsTyped(text))
        {
            return null;
        }

        var key = PromptText.WithoutIdeBlocks(text).Trim();
        if (key.Length == 0 || (_open is { FromEvent: true } open && string.Equals(open.Text, key, StringComparison.Ordinal)))
        {
            return null;
        }

        _open = (key, false, file.Entries.Count);
        return new TranscriptEntry(number, _now, EntryKind.Human, scrub(key), []);
    }

    private TranscriptEntry? Said(int number, CodexPayload payload)
    {
        var text = ContentBlocks.Read(payload.Content).Text.Trim();
        return text.Length == 0 ? null : new TranscriptEntry(number, _now, EntryKind.Assistant, scrub(text), []);
    }

    private TranscriptEntry Tool(int number, string name) => new(number, _now, EntryKind.Assistant, "", [name]);
}
