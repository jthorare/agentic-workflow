using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// The GitHub kinds the events feed doesn't carry (#3201, #2988): github.pr.checks and github.pr.conflict, from the open pull requests' state.
/// One poll reads GET /repos/{r}/pulls (open, most recently updated first) and, for each of the first <see cref="MaxPulls"/>, the head's check runs
/// (checks) or the pull request detail's mergeable state (conflict). The cursor is the state per pull request ({"42":"abc123:red"}), so it is saved
/// with the cursor only after the runs started, and a restart compares against the same state. A null cursor is a baseline: it stores the state and
/// fires nothing. A state that changed fires once: checks that finished red or green, a mergeable state that turned dirty. A state GitHub has not
/// worked out yet (checks still running, mergeable state 'unknown') fires nothing; for conflict it keeps the state stored before.
/// </summary>
/// <param name="client">Sends the requests with the connection's token, to api.github.com only.</param>
internal sealed class GitHubPullStates(PollerClient client)
{
    /// <summary>The most open pull requests one poll reads the state of (one more call each).</summary>
    internal const int MaxPulls = 20;

    private const string Host = "api.github.com";
    private const string Pending = "pending";

    private static readonly FrozenSet<string> _red = new[] { "failure", "timed_out", "cancelled", "action_required", "startup_failure" }.ToFrozenSet(StringComparer.Ordinal);

    internal async ValueTask<Result<PollResult>> Poll(Connection connection, string[] parts, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var root = $"https://{Host}/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}";
        var listed = await Get(connection, root + "/pulls?state=open&sort=updated&direction=desc&per_page=100", cancellationToken).ConfigureAwait(false);
        if (listed.IsError || listed.Value.Limited is not null)
        {
            return listed.IsError ? listed.Error : Limited(listed.Value.Limited!, cursor);
        }

        if (listed.Value.Body.ValueKind != JsonValueKind.Array)
        {
            return SindriErrors.ConnectionProviderChanged.With("the open pull requests answer is not a list");
        }

        var walk = new Walk(Snapshot(cursor), connection, root, parts[0] + "/" + parts[1], client.Clock.GetUtcNow());
        return await WalkAll(walk, kind, listed.Value.Body, cursor, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<PollResult>> WalkAll(Walk walk, EventKind kind, JsonElement pulls, string? cursor, CancellationToken cancellationToken)
    {
        foreach (var pull in pulls.EnumerateArray().Take(MaxPulls))
        {
            var step = await Observe(walk, kind, pull, cancellationToken).ConfigureAwait(false);
            if (step.IsError || step.Value.Limited is not null)
            {
                return step.IsError ? step.Error : Limited(step.Value.Limited!, cursor);
            }

            walk.Take(pull, step.Value);
        }

        return new PollResult([.. walk.Events.OrderBy(static e => e.OccurredAt).ThenBy(static e => e.DeliveryKey, StringComparer.Ordinal)], walk.Cursor(), RateLimited: false, RetryAfter: null);
    }

    private ValueTask<Result<Step>> Observe(Walk walk, EventKind kind, JsonElement pull, CancellationToken cancellationToken) =>
        kind == GitHubEventMapper.PrChecks ? Checks(walk, pull, cancellationToken) : Conflict(walk, pull, cancellationToken);

    // The head's check runs: any still running is pending; else any red conclusion is red; else green. No check runs at all is pending.
    private async ValueTask<Result<Step>> Checks(Walk walk, JsonElement pull, CancellationToken cancellationToken)
    {
        var head = PollJson.Text(PollJson.Path(pull, "head", "sha"));
        if (head is null)
        {
            return new Step(State: null, Event: null, Limited: null);
        }

        var read = await Get(walk.Connection, $"{walk.Root}/commits/{Uri.EscapeDataString(head)}/check-runs?per_page=100", cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value.Limited is not null)
        {
            return read.IsError ? read.Error : new Step(State: null, Event: null, read.Value.Limited);
        }

        var runs = PollJson.Member(read.Value.Body, "check_runs") is { ValueKind: JsonValueKind.Array } list ? list.EnumerateArray().ToList() : [];
        var conclusion = Conclusion(runs);
        if (string.Equals(conclusion, Pending, StringComparison.Ordinal))
        {
            return new Step(head + ":" + Pending, Event: null, Limited: null);
        }

        var failing = runs.Where(IsRed).Select(static r => PollJson.Text(PollJson.Member(r, "name")) ?? string.Empty).ToList();
        var at = runs.Select(static r => PollJson.Time(PollJson.Text(PollJson.Member(r, "completed_at")))).Max() ?? UpdatedAt(walk, pull);   // the last check that finished
        return new Step(head + ":" + conclusion, GitHubEventMapper.Checks(pull, walk.Repo, conclusion, failing, at), Limited: null);
    }

    // The detail's mergeable state: 'dirty' is a conflict, 'unknown' (or none) is not worked out yet, anything else is clean.
    private async ValueTask<Result<Step>> Conflict(Walk walk, JsonElement pull, CancellationToken cancellationToken)
    {
        var number = PollJson.Text(PollJson.Member(pull, "number"));
        if (number is null)
        {
            return new Step(State: null, Event: null, Limited: null);
        }

        var read = await Get(walk.Connection, $"{walk.Root}/pulls/{Uri.EscapeDataString(number)}", cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value.Limited is not null)
        {
            return read.IsError ? read.Error : new Step(State: null, Event: null, read.Value.Limited);
        }

        var detail = read.Value.Body;
        var state = PollJson.Text(PollJson.Member(detail, "mergeable_state"));
        var head = PollJson.Text(PollJson.Path(detail, "head", "sha")) ?? string.Empty;
        return state switch
        {
            null or "unknown" => new Step(State: null, Event: null, Limited: null),
            "dirty" => new Step(head + ":dirty", GitHubEventMapper.Conflict(detail, walk.Repo, UpdatedAt(walk, detail)), Limited: null),
            _ => new Step(head + ":clean", Event: null, Limited: null),
        };
    }

    private static string Conclusion(List<JsonElement> runs)
    {
        if (runs.Count == 0 || runs.Exists(static r => !string.Equals(PollJson.Text(PollJson.Member(r, "status")), "completed", StringComparison.Ordinal)))
        {
            return Pending;
        }

        return runs.Exists(IsRed) ? "red" : "green";
    }

    private static bool IsRed(JsonElement run) => PollJson.Text(PollJson.Member(run, "conclusion")) is { } conclusion && _red.Contains(conclusion);

    private static DateTimeOffset UpdatedAt(Walk walk, JsonElement pull) =>
        PollJson.Time(PollJson.Text(PollJson.Member(pull, "updated_at"))) ?? walk.Now;

    private async ValueTask<Result<Fetched>> Get(Connection connection, string url, CancellationToken cancellationToken)
    {
        var answer = await client.Send(connection, new HttpRequestMessage(HttpMethod.Get, url), cursorSent: false, cancellationToken).ConfigureAwait(false);
        if (answer.IsError || answer.Value.Body is not { } body)
        {
            return answer.IsError ? answer.Error : new Fetched(default, answer.Value);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return new Fetched(document.RootElement.Clone(), Limited: null);
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With("a pull request answer is not JSON");
        }
    }

    private static PollResult Limited(PollerAnswer answer, string? cursor) => new([], cursor, RateLimited: true, answer.RetryAfter, Reason: answer.Reason);

    // The cursor's state per pull request number, or null for a baseline (no cursor, or one that is not this poll's).
    private static Dictionary<string, string>? Snapshot(string? cursor)
    {
        if (cursor is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(cursor);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var saved = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                PollJson.Put(saved, entry.Name, PollJson.Text(entry.Value));
            }

            return saved;
        }
        catch (JsonException)   // boundary: a cursor of another shape is a fresh baseline
        {
            return null;
        }
    }

    private sealed record Fetched(JsonElement Body, PollerAnswer? Limited);

    private sealed record Step(string? State, PolledEvent? Event, PollerAnswer? Limited);

    // One poll's walk over the open pull requests: the state before, the state now, and the events that fire.
    private sealed class Walk(Dictionary<string, string>? previous, Connection connection, string root, string repo, DateTimeOffset now)
    {
        private readonly Dictionary<string, string> _next = new(StringComparer.Ordinal);

        internal Connection Connection { get; } = connection;

        internal string Root { get; } = root;

        internal string Repo { get; } = repo;

        internal DateTimeOffset Now { get; } = now;

        internal List<PolledEvent> Events { get; } = [];

        // why: a state not worked out yet keeps the one stored before, so 'clean -> unknown -> dirty' still fires
        internal void Take(JsonElement pull, Step step)
        {
            var number = PollJson.Text(PollJson.Member(pull, "number"));
            if (number is null)
            {
                return;
            }

            var before = previous?.GetValueOrDefault(number);
            var state = step.State ?? before;
            if (state is not null)
            {
                _next[number] = state;
            }

            if (previous is not null && step.Event is not null && !string.Equals(state, before, StringComparison.Ordinal))
            {
                Events.Add(step.Event);
            }
        }

        internal string Cursor() => new JsonObject([.. _next.Select(static p => KeyValuePair.Create(p.Key, (JsonNode?)JsonValue.Create(p.Value)))]).ToJsonString();
    }
}
