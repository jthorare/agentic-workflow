using System.Text.Json;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// The Codex usage reader: the 'token_count' events of the rollouts under <c lang="text">~/.codex/sessions</c>. A turn is counted from its 'last_token_usage' only
/// when the running total rose (the same turn is reported again), priced at the model the latest 'turn_context' named. Codex counts cached tokens inside
/// 'input_tokens', so they are split out here.
/// </summary>
/// <param name="root">The sessions directory.</param>
internal sealed class CodexUsage(string root) : IUsageReader
{
    private const string Counts = "\"token_count\"";
    private const string Unknown = "unknown";

    /// <inheritdoc/>
    public string Name => "codex";

    /// <inheritdoc/>
    public bool Verified => true;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Read(TranscriptFiles.Jsonl(root, from), (from, to), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) =>
        await Read([.. TranscriptFiles.Jsonl(root, since: null).Where(path => Path.GetFileNameWithoutExtension(path).EndsWith("-" + session.Value, StringComparison.Ordinal))], window: null, cancellationToken).ConfigureAwait(false);

    private static async ValueTask<IReadOnlyList<ModelTokens>> Read(IReadOnlyList<string> files, (DateTimeOffset From, DateTimeOffset To)? window, CancellationToken cancellationToken)
    {
        var turns = new List<ModelTokens>();
        foreach (var file in files)
        {
            var rollout = new Rollout();
            foreach (var line in await TranscriptFiles.Lines(file, "\"", cancellationToken).ConfigureAwait(false))
            {
                if (Fold(line, rollout, window) is { } turn)
                {
                    turns.Add(turn);
                }
            }
        }

        return UsageJson.PerModel(turns);
    }

    /// <summary>Takes one line of a rollout: a 'turn_context' sets the model, a rising 'token_count' inside the window is a turn.</summary>
    private static ModelTokens? Fold(string line, Rollout rollout, (DateTimeOffset From, DateTimeOffset To)? window)
    {
        if (!line.Contains(Counts, StringComparison.Ordinal) && !line.Contains("\"turn_context\"", StringComparison.Ordinal))
        {
            return null;
        }

        using var document = UsageJson.Parse(line);
        if (document is null || UsageJson.Object(document.RootElement, "payload") is not { } payload)
        {
            return null;
        }

        if (string.Equals(UsageJson.Text(document.RootElement, "type"), "turn_context", StringComparison.Ordinal))
        {
            rollout.Model = UsageJson.Text(payload, "model") ?? rollout.Model;
            return null;
        }

        return Turn(payload, rollout) is { } used && Inside(UsageJson.Timestamp(document.RootElement), window) ? used with { Model = rollout.Model } : null;
    }

    /// <summary>The turn a 'token_count' event adds, or null for another event or a total that did not rise.</summary>
    private static ModelTokens? Turn(JsonElement payload, Rollout rollout)
    {
        if (!string.Equals(UsageJson.Text(payload, "type"), "token_count", StringComparison.Ordinal) || UsageJson.Object(payload, "info") is not { } info
            || UsageJson.Object(info, "total_token_usage") is not { } total || UsageJson.Object(info, "last_token_usage") is not { } last)
        {
            return null;
        }

        var running = UsageJson.Count(total, "total_tokens");
        if (running <= rollout.Previous)
        {
            return null;
        }

        rollout.Previous = running;
        var cached = UsageJson.Count(last, "cached_input_tokens");
        return new ModelTokens(Unknown, Math.Max(UsageJson.Count(last, "input_tokens") - cached, 0), cached, CacheWrite: 0, UsageJson.Count(last, "output_tokens"));
    }

    private static bool Inside(DateTimeOffset? at, (DateTimeOffset From, DateTimeOffset To)? window) =>
        window is not { } span || (at is { } instant && instant >= span.From && instant < span.To);

    /// <summary>What reading a rollout remembers between its lines.</summary>
    private sealed class Rollout
    {
        public string Model { get; set; } = Unknown;

        public long Previous { get; set; } = -1;
    }
}
