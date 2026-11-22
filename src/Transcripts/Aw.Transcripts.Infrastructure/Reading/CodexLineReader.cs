using System.Text.Json;
using Aw.Transcripts.Core.Prompts;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Infrastructure.Wire;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>
/// Codex rollout lines. The first line is session_meta (id, cwd, source; a spawned agent's source is
/// '{ subagent: { thread_spawn: { parent_thread_id } } }'). A human prompt is an 'event_msg' 'user_message' of an
/// interactive (not 'exec') main session whose text passes <see cref="PromptText.IsTyped"/>; the 'response_item' user
/// messages repeat it with AGENTS.md and environment context around it and are not read. Assistant output is the
/// 'response_item' assistant messages and tool calls.
/// </summary>
/// <param name="clock">The file's write time, for a line with no timestamp before any line had one.</param>
/// <param name="scrub">The secret scrubber.</param>
internal sealed class CodexLineReader(DateTimeOffset clock, Func<string, string> scrub) : ILineReader
{
    private DateTimeOffset _now = clock;

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

        if (row.Payload is not { } payload)
        {
            return;
        }

        var entry = (row.Type, payload.Type) switch
        {
            ("session_meta", _) => Meta(payload, file),
            ("event_msg", "user_message") => Prompt(number, payload, file),
            ("response_item", "message") when payload.Role == "assistant" => Said(number, payload),
            ("response_item", "function_call" or "custom_tool_call") when payload.Name is { Length: > 0 } name => Tool(number, name),
            ("response_item", "local_shell_call") => Tool(number, "local_shell"),
            ("response_item", "web_search_call") => Tool(number, "web_search"),
            _ => null,
        };
        if (entry is not null)
        {
            file.Entries.Add(entry);
        }
    }

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

        return !file.IsSubagent && !file.NonInteractive && PromptText.IsTyped(text)
            ? new TranscriptEntry(number, _now, EntryKind.Human, scrub(text.Trim()), [])
            : new TranscriptEntry(number, _now, EntryKind.MachinePrompt, "", []);
    }

    private TranscriptEntry? Said(int number, CodexPayload payload)
    {
        var text = ContentBlocks.Read(payload.Content).Text.Trim();
        return text.Length == 0 ? null : new TranscriptEntry(number, _now, EntryKind.Assistant, scrub(text), []);
    }

    private TranscriptEntry Tool(int number, string name) => new(number, _now, EntryKind.Assistant, "", [name]);
}
