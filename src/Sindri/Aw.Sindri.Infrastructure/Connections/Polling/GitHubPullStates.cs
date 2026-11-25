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
/// One poll reads GET /repos/{r}/pulls (open, most recently updated first, one page of 100) and, for each of the first <see cref="MaxPulls"/>, the head's
/// check runs (checks) or the pull request detail's mergeable state (conflict). Check runs are not paged: only the first 100 runs of a head are read, and
/// legacy commit statuses (Jenkins and the like) are not read at all. One failed read fails the whole poll with the cursor unchanged.
/// The cursor is a JSON object: one entry per open pull request ({"42":"abc123:red"} for checks, {"42":"dirty:1"} for conflict, where 1 counts the times
/// it turned dirty) and "@at", the time of the poll that wrote it. A pull request past the cap keeps its entry from the poll before, and one that closed
/// drops out, so the cursor stays bounded. It is saved only after the runs started, so a restart compares against the same state.
/// A null cursor is a baseline: it stores the state and fires nothing. A state that changed fires once, only for a pull request that had an entry
/// before, or one created after "@at" (a cursor without "@at" is a baseline for every pull request not seen before). Checks that finished red or green
/// fire, and so does a mergeable state that turned dirty; a push that leaves a pull request dirty does not fire again. A state GitHub has not worked out
/// yet (checks still running, mergeable state 'unknown') fires nothing; for conflict it keeps the state stored before, or stores 'unknown:0' for a pull
/// request with none that opened after "@at", so its first dirty fires (an old one stores nothing: its first known state is the baseline). A pull
/// request that leaves the open list loses its count: if it reopens and turns dirty, spell 1 can repeat an old delivery id and be dropped as a duplicate.
/// </summary>
/// <param name="client">Sends the requests with the connection's token, to api.github.com only.</param>
internal sealed partial class GitHubPullStates(PollerClient client)
{
    /// <summary>The most open pull requests one poll reads the state of (one more call each).</summary>
    internal const int MaxPulls = 20;

    private const string Host = "api.github.com";
    private const string Pending = "pending";
    private const string Unknown = "unknown";

    private static readonly FrozenSet<string> _red = new[] { "failure", "timed_out", "cancelled", "action_required", "startup_failure" }.ToFrozenSet(StringComparer.Ordinal);

    internal async ValueTask<Result<PollResult>> Poll(Connection connection, string[] parts, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var root = $"https://{Host}/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}";
        var now = client.Clock.GetUtcNow();   // why: before the list read, so a pull request opened while it runs is after this poll's "@at"
        var listed = await Get(connection, root + "/pulls?state=open&sort=updated&direction=desc&per_page=100", cancellationToken).ConfigureAwait(false);
        if (listed.IsError || listed.Value.Limited is not null)
        {
            // why: the branch is taken only when IsError or Limited is not null, so Limited is set when it's read
            return listed.IsError ? listed.Error : Limited(listed.Value.Limited!, cursor);
        }

        if (listed.Value.Body.ValueKind != JsonValueKind.Array)
        {
            return SindriErrors.ConnectionProviderChanged.With("the open pull requests answer is not a list");
        }

        var walk = new Walk(Snapshot(cursor), connection, root, parts[0] + "/" + parts[1], now);
        return await WalkAll(walk, kind, listed.Value.Body, cursor, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<PollResult>> WalkAll(Walk walk, EventKind kind, JsonElement pulls, string? cursor, CancellationToken cancellationToken)
    {
        foreach (var pull in pulls.EnumerateArray().Skip(MaxPulls))
        {
            walk.Carry(pull);   // why: a pull request past the cap keeps its entry, or it would fire as new when it ranks back in
        }

        foreach (var pull in pulls.EnumerateArray().Take(MaxPulls))
        {
            var step = await Observe(walk, kind, pull, cancellationToken).ConfigureAwait(false);
            if (step.IsError || step.Value.Limited is not null)
            {
                // why: the branch is taken only when IsError or Limited is not null, so Limited is set when it's read
                return step.IsError ? step.Error : Limited(step.Value.Limited!, cursor);
            }

            walk.Take(pull, step.Value);
        }

        return new PollResult([.. walk.Events.OrderBy(static e => e.OccurredAt).ThenBy(static e => e.DeliveryKey, StringComparer.Ordinal)], walk.Cursor(), RateLimited: false, RetryAfter: null);
    }

    private ValueTask<Result<Step>> Observe(Walk walk, EventKind kind, JsonElement pull, CancellationToken cancellationToken) =>
        kind == EventKind.GitHubPrChecks ? Checks(walk, pull, cancellationToken) : Conflict(walk, pull, cancellationToken);

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
        var at = runs.Max(static r => PollJson.Time(PollJson.Text(PollJson.Member(r, "completed_at")))) ?? UpdatedAt(walk, pull);   // the last check that finished
        var runId = runs.Max(static r => long.TryParse(PollJson.Text(PollJson.Member(r, "id")), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0);   // in the delivery id: a re-run gets a new run id, so one that ends the same is a new event
        return new Step(head + ":" + conclusion, GitHubEventMapper.Checks(pull, walk.Repo, new CheckRunOutcome(conclusion, failing, at, runId)), Limited: null);
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

        return ConflictStep(walk, pull, number, read.Value.Body);
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
        private const string AtKey = "@at";   // why: not a pull request number, so it can't clash with an entry

        private readonly Dictionary<string, string> _next = new(StringComparer.Ordinal);
        private readonly DateTimeOffset? _since = PollJson.Time(previous?.GetValueOrDefault(AtKey));

        internal Connection Connection { get; } = connection;

        internal string Root { get; } = root;

        internal string Repo { get; } = repo;

        internal DateTimeOffset Now { get; } = now;

        internal List<PolledEvent> Events { get; } = [];

        internal string? Before(string number) => previous?.GetValueOrDefault(number);

        // A pull request the walk does not reach keeps what the last poll stored for it.
        internal void Carry(JsonElement pull)
        {
            if (PollJson.Text(PollJson.Member(pull, "number")) is { } number && Before(number) is { } kept)
            {
                _next[number] = kept;
            }
        }

        // why: a state not worked out yet keeps the one stored before, so 'clean -> unknown -> dirty' still fires
        internal void Take(JsonElement pull, Step step)
        {
            var number = PollJson.Text(PollJson.Member(pull, "number"));
            if (number is null)
            {
                return;
            }

            var before = Before(number);
            var state = step.State ?? before;
            if (state is not null)
            {
                _next[number] = state;
            }

            // why: no entry before is a baseline (it ranked past the cap, or the first poll), unless the pull request was opened after the last poll
            if (previous is not null && step.Event is not null && !string.Equals(state, before, StringComparison.Ordinal) && (before is not null || IsNew(pull)))
            {
                Events.Add(step.Event);
            }
        }

        internal string Cursor()
        {
            var cursor = new JsonObject();
            foreach (var (key, value) in _next)
            {
                cursor[key] = value;
            }

            cursor[AtKey] = Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            return cursor.ToJsonString();
        }

        // A pull request opened after the last poll's "@at"; with no "@at" (the first poll, or an older cursor) none is new.
        internal bool IsNew(JsonElement pull) =>
            _since is { } since && PollJson.Time(PollJson.Text(PollJson.Member(pull, "created_at"))) is { } created && created > since;
    }
}
