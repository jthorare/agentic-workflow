using System.Collections.Concurrent;
using System.Globalization;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// Once every verb is open to agent sessions (PR 2 of the agent-first spec), the ledger's actor is the only record of who did a write. This runs every
/// API golden scenario (their real bodies and seeding) as a session Sindri launched and records the method and path of each request it really sent.
/// A sent request is matched to a catalog write route by method and route template (a '{x}' segment matches any one segment; the template with the
/// most literal segments wins). Every event the calls appended must carry 'session:launch:&lt;launch id&gt;', never the human's, the host's default
/// 'host:sindri' or an unlabelled 'session:...'; and a catalog write counts as covered only when some scenario sent that method and template and
/// appended an event by the launch. A write that records no event, or records the wrong actor, fails here.
/// </summary>
public sealed class WriteAttributionTests
{
    private const string Launch = "LCH-0000000000000000000000000000ff01";

    // Write routes ("METHOD /route/template") this check skips, each with the reason; only a route that writes no ledger event belongs here.
    private static readonly Dictionary<string, string> _notSwept = new(StringComparer.Ordinal)
    {
        ["POST /sindri/stop"] = "stops the host, so the ledger can't be read after it; its actor is covered by the stop tests",
        ["POST /sindri/linear/connection"] = "a connection setting outside PR 2's list (LinearConnectionRules): the owner's",
        ["DELETE /sindri/linear/connection/write"] = "a connection setting outside PR 2's list (LinearConnectionRules): the owner's",
        ["POST /sindri/views/rebuild"] = "replays the ledger into views.db: it appends no event",
        ["POST /sindri/todos/note/check"] = "a dry check: writes nothing to the vault, the profile or the ledger (CheckTodoNoteService.cs:16)",
        ["POST /sindri/todos/note/set"] = "edits the live profile file through TodoNoteProfileEdit (SetTodoNoteService.cs:9): writes no ledger event",
        ["POST /sindri/todos/note/stop"] = "edits the live profile file through TodoNoteProfileEdit (StopTodoNoteService.cs:9): writes no ledger event",
        ["POST /sindri/linear/connection/teams"] = "reads the key's Linear teams (ListLinearTeamsService): stores nothing and writes no event",
        ["POST /sindri/workflow-runs/{run}/nodes/{node}/takeover"] = TakeoverReason,
        ["POST /sindri/workflow-runs/{run}/nodes/{node}/handback"] = TakeoverReason,
        ["POST /sindri/workflow/challenges/app"] = "refuses every text (IssueAppChallengeService.cs:17, no ports) and its endpoint appends nothing: writes no event",
        ["POST /sindri/workflows/validate"] = "checks a stored spec and returns its findings (ValidateWorkflowService): stores nothing and writes no event",
    };

    // Write routes that do append events but whose caller is proven another way than a launched session's header; not swept here.
    private static readonly Dictionary<string, string> _otherProof = new(StringComparer.Ordinal)
    {
        ["POST /sindri/sessions/{launchId}/heartbeat"] = WrapReason,
        ["POST /sindri/sessions/{launchId}/exit"] = WrapReason,
        ["POST /sindri/connections"] = "the store row carries the caller (Connection.CreatedBy, AddConnectionService.cs:169); proven by AddConnectionServiceTests.A_launched_session_adding_a_connection_is_the_rows_creator",
        ["POST /sindri/memories"] = "the store row carries the caller (MemoryInfo.CreatedBy, CreateMemoryService.cs:30); proven by MemoryReadsAndCreateTests.A_launched_session_creating_a_memory_is_its_creator",
        ["POST /sindri/memories/{name}/facts"] = AuditOnlyReason,
        ["DELETE /sindri/memories/{name}/facts"] = AuditOnlyReason,
        ["DELETE /sindri/connections/{name}"] = AuditOnlyReason,
        ["POST /sindri/connections/{name}/signin"] = AuditOnlyReason,
        ["POST /sindri/handoff"] = "the golden scenarios send it as the human; a launched session's handoff is proven by HandoffEndpointTests.A_launched_session_hands_off_without_the_human_token_and_keeps_its_actor (item.handoff's actor is session:launch:<id>)",
    };

    private const string TakeoverReason = "the node's held state lives in its Temporal workflow, so the route appends no ledger event (#2472); a session's call is proven by AgentNodeControlServiceTests";

    private const string AuditOnlyReason = "only the command.handled audit row carries the caller; the store keeps no actor (#2460)";

    private const string WrapReason = "the caller is the wrapper of the route's own launch, proven by that launch's secret; actor follow-up #2430";

    // Write routes no API golden scenario sends yet; the follow-up is #2434. A route here that a scenario now sends must move into the sweep.
    private static readonly Dictionary<string, string> _noGolden = new(StringComparer.Ordinal)
    {
        ["POST /sindri/context/map/checks"] = NoGoldenReason,
        ["POST /sindri/context/map/import"] = NoGoldenReason,
        ["POST /sindri/index/purge"] = NoGoldenReason,
        ["POST /sindri/sandbox/learn-off"] = NoGoldenReason,
        ["POST /sindri/sessions/{provider}/{id}/focus"] = NoGoldenReason,
        ["POST /sindri/launches/{launchId}/limit-hit"] = NoGoldenReason,
        ["POST /sindri/workflow/challenges"] = NoGoldenReason,
        ["POST /sindri/workflow/instances/{instanceId}/runs/{run}/complete"] = NoGoldenReason,
        ["POST /sindri/jobs/{jobId}/resume"] = NoGoldenReason,
        ["POST /sindri/todos/note/hold/restore"] = NoGoldenReason,
        ["POST /sindri/context/map/refresh"] = NoGoldenReason,
        ["POST /sindri/context/eval/backtest"] = NoGoldenReason,
        ["POST /sindri/context/sources/sync"] = NoGoldenReason,
        ["POST /sindri/index/sync"] = NoGoldenReason,
        ["POST /sindri/context/pages"] = NoGoldenReason,
        ["POST /sindri/context/pins"] = NoGoldenReason,
    };

    private const string NoGoldenReason = "no API golden scenario sends it yet (#2434)";

    // Write routes whose goldens are all refusals that write nothing; the succeeding scenario is #2434. An entry must be sent and must stay uncovered.
    private static readonly Dictionary<string, string> _onlyRefusals = new(StringComparer.Ordinal)
    {
        ["POST /sindri/workflows/{slug}/runs"] = "a run that starts needs the Temporal worker, which the golden host doesn't run; its goldens are refusals (#2434)",
        ["POST /sindri/setup/mcp"] = "a job starter; its only golden is not-in-profile, a refusal; the job's mcp.registered needs a succeeding scenario (#2434)",
        ["POST /sindri/setup/repos/onboard"] = "a job starter; its only golden is not-approved, a refusal; the job's repo.onboarded needs a succeeding scenario (#2434)",
    };

    [Fact(Timeout = 900_000), Trait("Lane", "integration")]
    public async Task Every_write_a_launched_session_makes_records_that_launch_as_its_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        var templates = await CatalogWriteTemplates(ct);
        var scenarios = ApiScenarios.All.Where(r => !IsNotASessionCall(r)).ToList();
        var sweep = new Sweep(templates);

        // why: each case starts its own host with file watchers; two at a time keeps the run under the box's inotify limit (128 per user)
        await Parallel.ForEachAsync(scenarios, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct }, async (row, token) =>
            await RunAsLaunchedSession(row, sweep, token));

        Assert.True(scenarios.Count > 0, "the sweep reached no scenario");
        var report = Problems(sweep, templates.Select(t => t.Key).ToHashSet(StringComparer.Ordinal));
        Assert.True(report.Count == 0, string.Join("\n\n", report));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_route_whose_only_event_by_the_launch_is_its_audit_row_stays_uncovered()
    {
        var sweep = new Sweep([new Template("POST", "/sindri/things/{id}"), new Template("POST", "/sindri/other")]);
        List<Callers.CallerRequests.SentRequest> sent = [new("POST", "/sindri/things/a", Task.FromResult(0L)), new("POST", "/sindri/other", Task.FromResult(2L))];
        List<LedgerEvent> events = [Event(1, EventTypes.CommandHandled), Event(2, "item.created"), Event(3, EventTypes.CommandHandled), Event(4, "item.created")];

        Cover(sweep, ("things", "refused"), sent, [0L, 1L], events);

        Assert.False(sweep.Covered.ContainsKey("POST /sindri/things/{id}"), "an audit row alone covered the route");
        Assert.Equal("things/refused (item.created)", sweep.Covered["POST /sindri/other"]);
    }

    private static LedgerEvent Event(long sequence, string type)
    {
        var envelope = new EventEnvelope<string>(
            Id: Guid.Empty, Type: type, SchemaVersion: 1, OccurredAt: DateTimeOffset.UnixEpoch, Actor: "session:launch:" + Launch, Tenant: "local", Ring: 0,
            CorrelationId: Guid.Empty, CausationId: null, TraceId: null, Payload: "{}");
        return new LedgerEvent(sequence, Epoch: 1, envelope, IdempotencyKey: type + ":" + sequence.ToString(CultureInfo.InvariantCulture), Item: null);
    }

    private static List<string> Problems(Sweep sweep, HashSet<string> catalog)
    {
        var sections = new List<(string Heading, List<string> Items)>
        {
            ("these scenarios threw as a launched session:", Sorted(sweep.Broken)),
            ($"these events were written during a launched session's calls without 'session:launch:{Launch}' as actor:", Sorted(sweep.Strays)),
            ("UNCOVERED: these catalog write routes appended no event by the launched session in any scenario that sent them (no event, no scenario, or another actor; a route that writes no event goes in _notSwept with its reason, one no golden sends goes in _noGolden with #2434):",
                Sorted(catalog.Where(k => !_notSwept.ContainsKey(k) && !_noGolden.ContainsKey(k) && !_otherProof.ContainsKey(k) && !_onlyRefusals.ContainsKey(k) && !sweep.Covered.ContainsKey(k)))),
            ("these _notSwept and _otherProof keys name no catalog write route (misspelt, or the route is gone):",
                Sorted(_notSwept.Keys.Concat(_otherProof.Keys).Where(k => !catalog.Contains(k)))),
            ("_noGolden guard: these entries are not a catalog write, or a scenario now sends them; move them into the sweep (delete the entry):",
                Sorted(_noGolden.Keys.Where(k => !catalog.Contains(k) || sweep.Sent.ContainsKey(k)))),
            ("these _noGolden and _onlyRefusals entries do not name #2434:",
                Sorted(_noGolden.Concat(_onlyRefusals).Where(e => !e.Value.Contains("#2434", StringComparison.Ordinal)).Select(e => e.Key))),
            ("_onlyRefusals guard: these entries are not a catalog write, no scenario sends them, or a scenario now covers them; move them into the sweep or _noGolden:",
                Sorted(_onlyRefusals.Keys.Select(k => (k, Why: OnlyRefusalBroken(k, catalog, sweep))).Where(e => e.Why is not null).Select(e => $"{e.k}: {e.Why}"))),
        };

        return [.. sections.Where(s => s.Items.Count > 0).Select(s => s.Heading + "\n" + string.Join('\n', s.Items))];
    }

    private static string? OnlyRefusalBroken(string key, HashSet<string> catalog, Sweep sweep) =>
        !catalog.Contains(key) ? "not a catalog write"
        : !sweep.Sent.ContainsKey(key) ? "no scenario sent it as the launch"
        : sweep.Covered.TryGetValue(key, out var by) ? "covered by " + by
        : null;

    private static List<string> Sorted(IEnumerable<string> items) => [.. items.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static async Task RunAsLaunchedSession((string Slug, string Scenario) row, Sweep sweep, CancellationToken token)
    {
        await using var host = await ApiScenarios.Start(row.Slug, row.Scenario, token);
        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();
        LaunchSecrets.Write(host.App.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, LaunchId.From(Launch), "proof");
        var before = (await ledger.ReadAfter(0, 100_000, token)).Value.Count;
        Exception? failure = null;
        IReadOnlyCollection<Callers.CallerRequests.SentRequest> sent;
        using (Callers.CallerRequests.AsLaunchedSession(Launch, async () => (await ledger.Head(token)).Value, out sent))   // the scenario's own call, with the seeding it does through Human(), as the launched session
        {
            try
            {
                _ = await ApiScenarios.Body(host, row.Slug, row.Scenario, token);
            }
            catch (Exception e) when (e is not OperationCanceledException)   // why: one run lists every broken case, not just the first
            {
                failure = e;
            }
        }

        var keys = sent.Select(s => MatchTemplate(sweep.Templates, s.Method, s.Path)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        keys.ForEach(k => sweep.Sent[k] = true);
        if (failure is not null)
        {
            sweep.Broken.Add($"{row.Slug}/{row.Scenario}: {failure.GetType().Name}: {failure.Message.ReplaceLineEndings(" ")}");
            return;
        }

        var marks = await Task.WhenAll(sent.Select(r => r.HeadBefore));
        var events = (await ledger.ReadAfter(0, 100_000, token)).Value.Skip(before).ToList();
        Cover(sweep, row, [.. sent], marks, events);

        // why: setup the human does before the session's first call (a typed-confirmation import) is not the session's; from that call on, every event must be the launch's
        var from = marks.Length == 0 ? long.MaxValue : marks[0];
        foreach (var envelope in events.Where(e => e.Sequence > from).Select(e => e.Envelope).Where(e => IsWrongActor(e.Actor)))
        {
            sweep.Strays.Add($"{row.Slug}/{row.Scenario}: {envelope.Type} by {envelope.Actor}");
        }
    }

    /// <summary>Each request sent as the launch covers its route when an event by the launch lands between its mark and the next request's, so no request's write can cover another's.</summary>
    // why: a write that lands after its own response (a job's later events) falls into the next request's window; harmless while every seeding job is
    // followed by a GET (FetchAfterBuild), which matches no write template; a seeding job followed by an own WRITE call is the gap noted on #2434
    private static void Cover(Sweep sweep, (string Slug, string Scenario) row, List<Callers.CallerRequests.SentRequest> sent, long[] marks, List<LedgerEvent> events)
    {
        for (var i = 0; i < sent.Count; i++)
        {
            var (start, end) = (marks[i], i + 1 < marks.Length ? marks[i + 1] : long.MaxValue);
            if (MatchTemplate(sweep.Templates, sent[i].Method, sent[i].Path) is { } key && events.Find(e => e.Sequence > start && e.Sequence <= end && IsByLaunch(e.Envelope.Actor) && !IsAuditRow(e)) is { } by)
            {
                sweep.Covered[key] = $"{row.Slug}/{row.Scenario} ({by.Envelope.Type})";
            }
        }
    }

    // why: every attempt, a refused one too, appends its 'command.handled' audit row with the caller's actor (AuditTrail.Record), so that row proves
    // nothing about the route's own write; strays still count it, so an audit row by the wrong actor fails
    private static bool IsAuditRow(LedgerEvent e) => string.Equals(e.Envelope.Type, EventTypes.CommandHandled, StringComparison.Ordinal);

    /// <summary>The catalog template a sent request belongs to: same method, same segment count, every literal segment equal; the most literal segments win.</summary>
    private static string? MatchTemplate(IReadOnlyList<Template> templates, string method, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return templates
            .Where(t => string.Equals(t.Method, method, StringComparison.Ordinal) && t.Matches(segments))
            .OrderByDescending(static t => t.Literals)
            .Select(static t => t.Key)
            .FirstOrDefault();
    }

    private static async Task<List<Template>> CatalogWriteTemplates(CancellationToken ct)
    {
        await using var host = await GoldenHost.Start(temporary: true, ct);
        return [.. host.App.Services.GetRequiredService<ICommandCatalog>().Rows
            .Where(IsWrite)
            .Select(static row => new Template(row.Method!, row.Route!))
            .DistinctBy(static t => t.Key, StringComparer.Ordinal)];
    }

    private static bool IsWrite(CommandRow row) =>
        (string.Equals(row.Method, "POST", StringComparison.Ordinal) || string.Equals(row.Method, "PUT", StringComparison.Ordinal) || string.Equals(row.Method, "DELETE", StringComparison.Ordinal) || string.Equals(row.Method, "PATCH", StringComparison.Ordinal))
        && row.Route is { Length: > 1 }
        && row.Class is not (ActionClass.Local or ActionClass.StepReport);

    private sealed record Template(string Method, string Route)
    {
        private readonly string[] _segments = Route.Split('/', StringSplitOptions.RemoveEmptyEntries);

        public string Key => Method + " " + Route;

        public int Literals => _segments.Count(static s => !IsParameter(s));

        public bool Matches(string[] sent) =>
            sent.Length == _segments.Length && _segments.Zip(sent).All(static p => IsParameter(p.First) || string.Equals(p.First, p.Second, StringComparison.Ordinal));

        private static bool IsParameter(string segment) => segment.StartsWith('{') && segment.EndsWith('}');
    }

    private sealed class Sweep(IReadOnlyList<Template> templates)
    {
        public IReadOnlyList<Template> Templates { get; } = templates;

        /// <summary>Catalog write keys some scenario sent, whether or not the call appended an event.</summary>
        public ConcurrentDictionary<string, bool> Sent { get; } = new(StringComparer.Ordinal);

        /// <summary>Catalog write keys sent by a scenario whose calls appended an event by the launch; the value names one such scenario and the event type.</summary>
        public ConcurrentDictionary<string, string> Covered { get; } = new(StringComparer.Ordinal);

        public ConcurrentBag<string> Strays { get; } = [];

        public ConcurrentBag<string> Broken { get; } = [];
    }

    /// <summary>
    /// Scenarios about the human's own credentials or an app origin: a launched session has no such reading. Also the 'session wrap' routes: the
    /// caller there is the wrapper of the route's own launch, proven by that launch's secret (X-Sindri-Launch-Secret), never another launched session.
    /// </summary>
    private static bool IsNotASessionCall((string Slug, string Scenario) row) =>
        string.Equals(row.Slug, "sessions-id-heartbeat", StringComparison.Ordinal)
        || string.Equals(row.Slug, "sessions-id-exit", StringComparison.Ordinal)
        || row.Scenario.StartsWith("human-", StringComparison.Ordinal)
        || row.Scenario.StartsWith("prover-", StringComparison.Ordinal)
        || row.Scenario.StartsWith("unknown-caller", StringComparison.Ordinal)
        || row.Scenario.StartsWith("refused-origin", StringComparison.Ordinal);

    private static bool IsByLaunch(string actor) => string.Equals(actor, "session:launch:" + Launch, StringComparison.Ordinal);

    /// <summary>A launched session's call must not leave the human's actor, the scope's default host actor, or a session actor without the launch.</summary>
    private static bool IsWrongActor(string actor) =>
        !IsByLaunch(actor)
        && (actor.StartsWith("human:", StringComparison.Ordinal) || actor.StartsWith("session:", StringComparison.Ordinal) || string.Equals(actor, "host:sindri", StringComparison.Ordinal));
}
