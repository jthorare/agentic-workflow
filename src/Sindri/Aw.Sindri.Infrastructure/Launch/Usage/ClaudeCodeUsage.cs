using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// The Claude Code usage reader: the assistant messages of the transcripts under <c lang="text">~/.claude/projects</c>, each message counted once by its id (a
/// message is written again as it streams; the last copy has the final output count), subagents' transcripts included.
/// </summary>
/// <param name="root">The owner's projects directory.</param>
/// <param name="alsoUnder">The projects directories of the separate agent homes (a separate-login launch writes there); empty for an owner-copy box.</param>
#pragma warning disable CA1001 // the semaphore is never waited on through a handle, so it holds nothing to release; the reader lives as long as the process
internal sealed class ClaudeCodeUsage(string root, IReadOnlyList<string> alsoUnder) : IUsageReader
{
    private const string Marker = "\"usage\"";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, FileState> _files = new(StringComparer.Ordinal);
    private readonly SeenWindow _seen = new();

    /// <summary>What was read of one transcript: the byte just past its last complete line, and each message by id with its timestamp.</summary>
    private sealed class FileState
    {
        public long Offset { get; set; }

        public Dictionary<string, (DateTimeOffset? At, ModelTokens Tokens)> Messages { get; } = new(StringComparer.Ordinal);
    }

    public ClaudeCodeUsage(string root) : this(root, [])
    {
    }

    /// <inheritdoc/>
    public string Name => "claude-code";

    /// <inheritdoc/>
    public bool Verified => true;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        await Task.Yield();   // the directory walk below never runs on the caller's thread
        // incremental (#2777): per file the bytes already read and the messages found, so a later call parses only what was appended
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var files = TranscriptFiles.Jsonl([root, .. alsoUnder], _seen.Widen(from, to));
            foreach (var gone in _files.Keys.Except(files, StringComparer.Ordinal).ToList())
            {
                _ = _files.Remove(gone);
            }

            var byMessage = new Dictionary<string, ModelTokens>(StringComparer.Ordinal);
            foreach (var path in files)
            {
                var state = _files.TryGetValue(path, out var known) ? known : _files[path] = new FileState();
                await Refresh(path, state, cancellationToken).ConfigureAwait(false);
                foreach (var (id, message) in state.Messages)
                {
                    if (Inside(message.At, (from, to)))
                    {
                        byMessage[id] = message.Tokens;
                    }
                }
            }

            return UsageJson.PerModel(byMessage.Values);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Reads what was appended to one transcript since the last call (all of it again when the file got smaller).</summary>
    private static async ValueTask Refresh(string path, FileState state, CancellationToken cancellationToken)
    {
        var (lines, offset, restarted) = await TranscriptFiles.LinesFrom(path, state.Offset, Marker, cancellationToken).ConfigureAwait(false);
        if (restarted)
        {
            state.Messages.Clear();
        }

        foreach (var line in lines)
        {
            using var document = UsageJson.Parse(line);
            if (document is not null && Counted(document.RootElement) is { } counted)
            {
                state.Messages[counted.Id] = (UsageJson.Timestamp(document.RootElement), counted.Tokens);
            }
        }

        state.Offset = offset;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) =>
        await Read([.. TranscriptFiles.Jsonl([root, .. alsoUnder], since: null).Where(path => UsageJson.OfSession(path, session.Value))], window: null, cancellationToken).ConfigureAwait(false);

    private static async ValueTask<IReadOnlyList<ModelTokens>> Read(IReadOnlyList<string> files, (DateTimeOffset From, DateTimeOffset To)? window, CancellationToken cancellationToken)
    {
        var byMessage = new Dictionary<string, ModelTokens>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var line in await TranscriptFiles.Lines(file, Marker, cancellationToken).ConfigureAwait(false))
            {
                using var document = UsageJson.Parse(line);
                if (document is not null && Message(document.RootElement, window) is { } counted)
                {
                    byMessage[counted.Id] = counted.Tokens;
                }
            }
        }

        return UsageJson.PerModel(byMessage.Values);
    }

    private static (string Id, ModelTokens Tokens)? Message(System.Text.Json.JsonElement line, (DateTimeOffset From, DateTimeOffset To)? window) =>
        Inside(UsageJson.Timestamp(line), window) ? Counted(line) : null;

    private static bool Inside(DateTimeOffset? at, (DateTimeOffset From, DateTimeOffset To)? window) => window is not { } span || (at is { } instant && instant >= span.From && instant < span.To);

    /// <summary>The message an assistant line carries, with the tokens it used; null for any other line and for the synthetic messages the CLI invents.</summary>
    private static (string Id, ModelTokens Tokens)? Counted(System.Text.Json.JsonElement line)
    {
        if (!string.Equals(UsageJson.Text(line, "type"), "assistant", StringComparison.Ordinal) || UsageJson.Object(line, "message") is not { } message
            || UsageJson.Object(message, "usage") is not { } usage || UsageJson.Text(message, "id") is not { } id || UsageJson.Text(message, "model") is not { } model)
        {
            return null;
        }

        return string.Equals(model, "<synthetic>", StringComparison.Ordinal)
            ? null
            : (id, new ModelTokens(model, UsageJson.Count(usage, "input_tokens"), UsageJson.Count(usage, "cache_read_input_tokens"), UsageJson.Count(usage, "cache_creation_input_tokens"), UsageJson.Count(usage, "output_tokens")));
    }
}
