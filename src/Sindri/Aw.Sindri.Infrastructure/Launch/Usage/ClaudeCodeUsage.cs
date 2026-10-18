using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// The Claude Code usage reader: the assistant messages of the transcripts under <c lang="text">~/.claude/projects</c>, each message counted once by its id (a
/// message is written again as it streams; the last copy has the final output count), subagents' transcripts included.
/// </summary>
/// <param name="root">The projects directory.</param>
internal sealed class ClaudeCodeUsage(string root) : IUsageReader
{
    private const string Marker = "\"usage\"";

    /// <inheritdoc/>
    public string Name => "claude-code";

    /// <inheritdoc/>
    public bool Verified => true;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Read(TranscriptFiles.Jsonl(root, from), (from, to), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) =>
        await Read([.. TranscriptFiles.Jsonl(root, since: null).Where(path => UsageJson.OfSession(path, session.Value))], window: null, cancellationToken).ConfigureAwait(false);

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
