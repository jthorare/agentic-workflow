using System.Globalization;
using System.Text.Json;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Transcripts;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.Transcripts;

/// <summary>
/// ITranscriptReader over the Claude transcripts under ~/.claude/projects (&lt;projects&gt;/&lt;project&gt;/&lt;session&gt;.jsonl). A
/// transcript is untrusted data: it is parsed, never followed, and a line that isn't a JSON object is skipped.
/// </summary>
/// <param name="projectsDirectory">The projects directory.</param>
internal sealed class ClaudeTranscriptReader(string projectsDirectory) : ITranscriptReader
{
    private const string UnknownSession = "unknown";

    /// <inheritdoc />
    public ValueTask<Result<Transcript>> FindSession(SessionId session, CancellationToken cancellationToken)
    {
        var name = session.Value;
        if (!IsPlainName(name))
        {
            return Missing($"no transcript for session '{name}'");
        }

        var file = Candidates(name).FirstOrDefault(File.Exists);
        return file is null ? Missing($"no transcript for session '{name}'") : Read(file, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Transcript>> Read(string path, CancellationToken cancellationToken)
    {
        string text;
        DateTimeOffset modifiedAt;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return JudgeErrors.NoTranscript.With($"can't read the transcript '{path}': {exception.Message}");
        }

        return Parse(text, modifiedAt);
    }

    // A session id names a file in a project directory, never a path.
    private static bool IsPlainName(string name) =>
        name.Length > 0
        && !name.Contains('/', StringComparison.Ordinal)
        && !name.Contains('\\', StringComparison.Ordinal)
        && !string.Equals(name, ".", StringComparison.Ordinal)
        && !string.Equals(name, "..", StringComparison.Ordinal);

    private static Result<Transcript> Parse(string text, DateTimeOffset modifiedAt)
    {
        var rows = new RowReader();
        foreach (var line in text.Split('\n'))
        {
            if (line.AsSpan().Trim().IsEmpty)
            {
                continue;
            }

            using var document = ParseRow(line);
            if (document is not null)
            {
                rows.Add(document.RootElement);
            }
        }

        return rows.Build(modifiedAt);
    }

    // The line as a JSON object, or null when it is not one.
    private static JsonDocument? ParseRow(string line)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return document;
        }

        document.Dispose();
        return null;
    }

    // The text of a user message, or null for a tool result or a content shape that carries no text.
    private static string? PromptText(JsonElement row)
    {
        if (!row.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var blocks = content.EnumerateArray().Where(b => b.ValueKind == JsonValueKind.Object).ToList();
        if (blocks.Exists(b => Text(b, "type") == "tool_result"))
        {
            return null;
        }

        var texts = blocks.Where(b => Text(b, "type") == "text").Select(b => Text(b, "text") ?? "").ToList();
        return texts.Count == 0 ? null : string.Join('\n', texts);
    }

    // The Edit, Write and MultiEdit calls of an assistant row; any other tool call is not an edit.
    private static IEnumerable<TranscriptEdit> EditsOf(JsonElement row)
    {
        if (!row.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return content.EnumerateArray()
            .Where(b => b.ValueKind == JsonValueKind.Object && Text(b, "type") == "tool_use")
            .SelectMany(EditsOfCall);
    }

    private static IEnumerable<TranscriptEdit> EditsOfCall(JsonElement call)
    {
        if (!call.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var file = Text(input, "file_path") ?? "";
        return Text(call, "name") switch
        {
            "Edit" => [Replacement(file, input)],
            "Write" => [new TranscriptEdit(file, "", Text(input, "content") ?? "")],
            "MultiEdit" => MultiEdit(file, input),
            _ => [],
        };
    }

    private static IEnumerable<TranscriptEdit> MultiEdit(string file, JsonElement input) =>
        input.TryGetProperty("edits", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).Select(e => Replacement(file, e))
            : [];

    private static TranscriptEdit Replacement(string file, JsonElement change) =>
        new(file, Text(change, "old_string") ?? "", Text(change, "new_string") ?? "");

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Timestamp(JsonElement row) =>
        Text(row, "timestamp") is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;

    private static ValueTask<Result<Transcript>> Missing(string detail) =>
        ValueTask.FromResult<Result<Transcript>>(JudgeErrors.NoTranscript.With(detail));

    // Fails open: a missing or unreadable projects directory yields no candidates.
    private IEnumerable<string> Candidates(string name)
    {
        string[] directories;
        try
        {
            directories = Directory.GetDirectories(projectsDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return directories.Select(directory => Path.Combine(directory, name + ".jsonl"));
    }

    // Collects the rows of one transcript: prompts, turns with their edits, and where each session ends.
    private sealed class RowReader
    {
        private readonly List<TranscriptPrompt> _prompts = [];
        private readonly List<TurnBuilder> _turns = [];
        private readonly Dictionary<SessionId, DateTimeOffset> _ends = [];
        private readonly Dictionary<string, TurnBuilder> _current = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        public void Add(JsonElement row)
        {
            var session = Text(row, "sessionId") is { Length: > 0 and <= 4096 } id ? id : UnknownSession;
            var at = Timestamp(row);
            if (at is { } stamp)
            {
                var key = SessionId.From(session);
                _ends[key] = _ends.TryGetValue(key, out var seen) && seen > stamp ? seen : stamp;
            }

            var type = Text(row, "type");
            if (string.Equals(type, "user", StringComparison.Ordinal))
            {
                AddUser(row, session, at);
            }
            else if (string.Equals(type, "assistant", StringComparison.Ordinal) && _current.TryGetValue(session, out var open))
            {
                open.Edits.AddRange(EditsOf(row));
            }
        }

        public Result<Transcript> Build(DateTimeOffset modifiedAt) =>
            new Transcript(modifiedAt, _prompts, [.. _turns.Select(t => new TranscriptTurn(t.Session, t.Index, t.Prompt, t.Edits))], _ends);

        private void AddUser(JsonElement row, string session, DateTimeOffset? at)
        {
            if (PromptText(row) is not { } prompt || !TurnOutcomes.IsRealPromptText(prompt))
            {
                return;
            }

            if (at is { } sent)
            {
                _prompts.Add(new TranscriptPrompt(sent, prompt));
            }

            if (prompt.AsSpan().Trim().IsEmpty)
            {
                return;
            }

            var index = _counts.GetValueOrDefault(session);
            _counts[session] = index + 1;
            var turn = new TurnBuilder(SessionId.From(session), index, prompt);
            _current[session] = turn;
            _turns.Add(turn);
        }
    }

    private sealed class TurnBuilder(SessionId session, int index, string prompt)
    {
        public SessionId Session { get; } = session;

        public int Index { get; } = index;

        public string Prompt { get; } = prompt;

        public List<TranscriptEdit> Edits { get; } = [];
    }
}
