using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// The Codex usage reader: the 'token_count' events of the rollouts under <c lang="text">~/.codex/sessions</c>. A turn is counted from its 'last_token_usage' only
/// when the running total rose (the same turn is reported again), priced at the model the latest 'turn_context' named. Codex counts cached tokens inside
/// 'input_tokens', so they are split out here.
/// </summary>
/// <param name="root">The owner's sessions directory.</param>
/// <param name="alsoUnder">The sessions directories of the separate agent homes (a separate-login launch writes there); empty for an owner-copy box.</param>
#pragma warning disable CA1001 // the semaphore is never waited on through a handle, so it holds nothing to release; the reader lives as long as the process
internal sealed class CodexUsage(string root, IReadOnlyList<string> alsoUnder) : IUsageReader
{
    private const string Counts = "\"token_count\"";
    private const string Unknown = "unknown";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, FileState> _files = new(StringComparer.Ordinal);
    private readonly SeenWindow _seen = new();


    public CodexUsage(string root) : this(root, [])
    {
    }

    /// <inheritdoc/>
    public string Name => "codex";

    /// <inheritdoc/>
    public bool Verified => true;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        // incremental (#3129): per rollout the bytes already read, the model and running total so far, and the turns found
        await Task.Yield();   // the directory walk below never runs on the caller's thread
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var files = TranscriptFiles.Jsonl([root, .. alsoUnder], _seen.Widen(from, to));
            foreach (var gone in _files.Keys.Except(files, StringComparer.Ordinal).ToList())
            {
                _ = _files.Remove(gone);
            }

            var turns = new List<ModelTokens>();
            foreach (var path in files)
            {
                var state = _files.TryGetValue(path, out var known) ? known : _files[path] = new FileState();
                await Refresh(path, state, cancellationToken).ConfigureAwait(false);
                turns.AddRange(state.Turns.Where(turn => Inside(turn.At, (from, to))).Select(static turn => turn.Tokens));
            }

            return UsageJson.PerModel(turns);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken)
    {
        var turns = new List<ModelTokens>();
        foreach (var file in TranscriptFiles.Jsonl([root, .. alsoUnder], since: null).Where(path => Path.GetFileNameWithoutExtension(path).EndsWith("-" + session.Value, StringComparison.Ordinal)))
        {
            var rollout = new Rollout();
            foreach (var line in await TranscriptFiles.Lines(file, "\"", cancellationToken).ConfigureAwait(false))
            {
                if (Fold(line, rollout) is { } turn)
                {
                    turns.Add(turn.Tokens);
                }
            }
        }

        return UsageJson.PerModel(turns);
    }

    /// <summary>Reads what was appended to one rollout since the last call (all of it again, with fresh state, when the file got smaller).</summary>
    private static async ValueTask Refresh(string path, FileState state, CancellationToken cancellationToken)
    {
        var (lines, offset, restarted) = await TranscriptFiles.LinesFrom(path, state.Offset, "\"", cancellationToken).ConfigureAwait(false);
        if (restarted)
        {
            state.Rollout = new Rollout();
            state.Turns.Clear();
        }

        foreach (var line in lines)
        {
            if (Fold(line, state.Rollout) is { } turn)
            {
                state.Turns.Add(turn);
            }
        }

        state.Offset = offset;
    }

    /// <summary>Takes one line of a rollout: a 'turn_context' sets the model, a rising 'token_count' is a turn (with its timestamp; the window is applied when asked).</summary>
    private static (DateTimeOffset? At, ModelTokens Tokens)? Fold(string line, Rollout rollout)
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

        return Turn(payload, rollout) is { } used ? (UsageJson.Timestamp(document.RootElement), used with { Model = rollout.Model }) : null;
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

    /// <summary>What was read of one rollout: the byte just past its last complete line, what the lines so far set (model, running total), and each turn with its timestamp.</summary>
    private sealed class FileState
    {
        public long Offset { get; set; }

        public Rollout Rollout { get; set; } = new();

        public List<(DateTimeOffset? At, ModelTokens Tokens)> Turns { get; } = [];
    }

    /// <summary>What reading a rollout remembers between its lines.</summary>
    private sealed class Rollout
    {
        public string Model { get; set; } = Unknown;

        public long Previous { get; set; } = -1;
    }
}
