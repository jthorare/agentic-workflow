using System.Net;
using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Features.StartSkillRun;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri skill run' (skill flows spec §7): the item it makes for the trial does not stay open when the run is refused.</summary>
public sealed class CliSkillRunTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_run_closes_its_item_as_cancelled_and_exits_with_the_refusal()
    {
        var item = A.FirstItemId.Value;
        var api = new RoutedApi().On("/sindri/status", HttpStatusCode.OK, RoutedApi.StatusAtHead7)
            .On("/sindri/items", HttpStatusCode.OK, new ItemCreatedResponse(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 4), A.FirstItemId))
            .OnRaw("/sindri/skills/review/runs", HttpStatusCode.BadRequest, """{"title":"SND-CLI-003","status":400,"detail":"block 'review' needs input 'pr'"}""")
            .On("/sindri/items/" + item, HttpStatusCode.OK, View(version: 1))
            .On("/sindri/items/" + item + "/close", HttpStatusCode.OK, new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 5));

        var code = await Verb(api).Run(["review"], Token);

        Assert.Equal(2, code);
        Assert.Contains("needs input 'pr'", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("""{"reason":"cancelled","itemVersion":1}""", api.Calls[^1].Body);
        Assert.Equal("/sindri/items/" + item + "/close", api.Calls[^1].Path);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_result_line_is_the_newest_skill_launchs_result_file()
    {
        var api = new RoutedApi().On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage(
        [
            Started(1, """{"launchId":"LCH-1","skill":{"name":"review","resultFile":"/run/old/result-LCH-1.json"}}"""),
            Started(2, """{"launchId":"LCH-2"}"""),
            Started(3, """{"launchId":"LCH-3","skill":{"name":"review","resultFile":"/run/new/result-LCH-3.json"}}"""),
        ], Next: null));

        var path = await Verb(api).ResultFileOf(A.FirstItemId.Value, 0, Token);

        Assert.Equal("/run/new/result-LCH-3.json", path);
        Assert.StartsWith("?item=" + A.FirstItemId.Value + "&type=launch.started", api.Calls[^1].Query, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_launch_with_a_result_file_or_an_unreadable_ledger_leaves_the_line_out()
    {
        var none = new RoutedApi().On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage([Started(1, """{"launchId":"LCH-1"}""")], Next: null));
        var down = new RoutedApi().OnRaw("/sindri/ledger", HttpStatusCode.ServiceUnavailable, """{"title":"SND-API-011","detail":"views are behind"}""");

        Assert.Equal(string.Empty, await Verb(none).ResultFileOf(A.FirstItemId.Value, 0, Token));
        Assert.Equal(string.Empty, await Verb(down).ResultFileOf(A.FirstItemId.Value, 0, Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_failed_before_launch_reports_the_newest_launch_refusal_with_its_code()
    {
        // why: the failed transition's reason is only the class ('launch'); the refusal and its fix live on launch.failed (found in F-1's hand test)
        var api = new RoutedApi().On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage(
        [
            Event(1, "launch.failed", """{"launchId":"LCH-1","code":"SND-SKILL-007","message":"older"}"""),
            Event(2, "launch.failed", """{"launchId":"LCH-2","code":"SND-SKILL-005","message":"review needs MCP server prism-mcp (register it with the agent)"}"""),
        ], Next: null));

        var line = await Verb(api).LaunchRefusalOf(A.FirstItemId.Value, 0, Token);

        Assert.Equal("SND-SKILL-005 review needs MCP server prism-mcp (register it with the agent)", line);
        Assert.StartsWith("?item=" + A.FirstItemId.Value + "&type=launch.failed", api.Calls[^1].Query, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_launch_refusal_or_an_unreadable_ledger_gives_none()
    {
        var none = new RoutedApi().On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage([], Next: null));
        var down = new RoutedApi().OnRaw("/sindri/ledger", HttpStatusCode.ServiceUnavailable, """{"title":"SND-API-011","detail":"views are behind"}""");

        Assert.Null(await Verb(none).LaunchRefusalOf(A.FirstItemId.Value, 0, Token));
        Assert.Null(await Verb(down).LaunchRefusalOf(A.FirstItemId.Value, 0, Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_that_ended_on_its_own_reports_its_exit_code()
    {
        var api = new RoutedApi().On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage(
        [
            Event(1, "session.ended", """{"launch":"LCH-1","reason":"exit","exitCode":0}"""),
            Event(2, "session.ended", """{"launch":"LCH-2","reason":"exit","exitCode":3}"""),
        ], Next: null));

        var line = await Verb(api).SessionEndOf(A.FirstItemId.Value, 0, Token);

        Assert.Equal("session exited 3", line);
        Assert.StartsWith("?item=" + A.FirstItemId.Value + "&type=session.ended", api.Calls[^1].Query, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_session_end_or_an_unreadable_ledger_gives_none()
    {
        var none = new RoutedApi().On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage([], Next: null));
        var down = new RoutedApi().OnRaw("/sindri/ledger", HttpStatusCode.ServiceUnavailable, """{"title":"SND-API-011","detail":"views are behind"}""");

        Assert.Null(await Verb(none).SessionEndOf(A.FirstItemId.Value, 0, Token));
        Assert.Null(await Verb(down).SessionEndOf(A.FirstItemId.Value, 0, Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_produced_output_prints_its_value_not_its_hash()
    {
        var api = FollowedRun().On(FactRoute, HttpStatusCode.OK, FactValue("\"" + FindingsPath + "\""));

        var code = await Verb(api).Run(["review", "--follow"], Token);

        Assert.Equal(0, code);
        Assert.Contains($"fact:reviewFindings = {FindingsPath}", _out.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sha256:c16c", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_followed_run_prints_the_pass_golden_with_its_fact_read_through_the_facts_route()
    {
        // why: the golden's fact used to be handed to SkillRunText as a value, so it could not tell a value from a hash (#2148)
        var api = FollowedRun().On(FactRoute, HttpStatusCode.OK, FactValue("\"" + FindingsPath + "\""));

        _ = await Verb(api).Run(["review", "--follow"], Token);

        Golden.Match(_out.ToString(), typeof(CliSkillRunTests).Assembly, "Skills/Golden/skill-run-pass.txt", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_object_value_prints_as_raw_json()
    {
        var api = FollowedRun().On(FactRoute, HttpStatusCode.OK, FactValue("""{"findings":2}"""));

        _ = await Verb(api).Run(["review", "--follow"], Token);

        Assert.Contains("""fact:reviewFindings = {"findings":2}""", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_fact_value_prints_the_key_and_a_reason()
    {
        var readable = FollowedRun().On(FactRoute, HttpStatusCode.OK, FactValue("\"" + FindingsPath + "\""));
        var unreadable = FollowedRun().OnRaw(FactRoute, HttpStatusCode.NotFound, """{"title":"SND-ITEM-404","detail":"no such fact"}""");

        var readableCode = await Verb(readable).Run(["review", "--follow"], Token);
        _out.GetStringBuilder().Clear();
        var code = await Verb(unreadable).Run(["review", "--follow"], Token);

        Assert.Equal(readableCode, code);
        Assert.Contains("fact:reviewFindings = (unreadable: ", _out.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sha256:c16c", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launch_refusal_past_the_first_ledger_page_is_printed()
    {
        var api = new RoutedApi().OnPages(LedgerRoute, [(0, new LedgerPage([], Next: 5000)), (5000, new LedgerPage([Event(5687, "launch.failed", """{"launchId":"LCH-1","code":"SND-SKILL-007","message":"no such agent"}""")], Next: null))]);

        Assert.Equal("SND-SKILL-007 no such agent", await Verb(api).LaunchRefusalOf(A.FirstItemId.Value, 0, Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_end_past_the_first_ledger_page_is_printed()
    {
        var api = new RoutedApi().OnPages(LedgerRoute, [(0, new LedgerPage([], Next: 5000)), (5000, new LedgerPage([Event(5700, "session.ended", """{"launch":"LCH-1","reason":"exit","exitCode":3}""")], Next: null))]);

        Assert.Equal("session exited 3", await Verb(api).SessionEndOf(A.FirstItemId.Value, 0, Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_result_file_past_the_first_ledger_page_is_printed()
    {
        var api = new RoutedApi().OnPages(LedgerRoute, [(0, new LedgerPage([], Next: 5000)), (5000, new LedgerPage([Started(5690, """{"launchId":"LCH-1","skill":{"name":"review","resultFile":"/run/late/result-LCH-1.json"}}""")], Next: null))]);

        Assert.Equal("/run/late/result-LCH-1.json", await Verb(api).ResultFileOf(A.FirstItemId.Value, 0, Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_empty_ledger_pages_before_the_hit_do_not_end_the_read()
    {
        var api = new RoutedApi().OnPages(LedgerRoute,
        [
            (0, new LedgerPage([], Next: 5000)),
            (5000, new LedgerPage([], Next: 10000)),
            (10000, new LedgerPage([Event(10400, "session.ended", """{"launch":"LCH-1","reason":"exit","exitCode":1}""")], Next: null)),
        ]);

        Assert.Equal("session exited 1", await Verb(api).SessionEndOf(A.FirstItemId.Value, 0, Token));
        Assert.Equal(3, api.Calls.Count(static c => c.Path == LedgerRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_ledger_read_starts_at_the_trials_item_not_at_zero()
    {
        // why: the item is made for the trial, so its first row is at the sequence the create was accepted at (4); reading from 0 scans every older row first
        var api = FollowedRun().OnPages(LedgerRoute, [(3, new LedgerPage([Started(5, """{"launchId":"LCH-1","skill":{"name":"review","resultFile":"/run/x/result-LCH-1.json"}}""")], Next: null))])
            .On(FactRoute, HttpStatusCode.OK, FactValue("\"" + FindingsPath + "\""));

        _ = await Verb(api).Run(["review", "--follow"], Token);

        var asked = api.Calls.Where(static c => c.Path == LedgerRoute).Select(static c => c.Query).ToList();
        Assert.NotEmpty(asked);
        Assert.All(asked, static q => Assert.Contains("&after=3", q, StringComparison.Ordinal));
    }

    private const string LedgerRoute = "/sindri/ledger";

    private const string ManifestHash = "9f2c4be1a07d58a3c1e6b4d9027f83aa5c61d0e4b7f29a8c3d5e1f60b2a47c98";
    private const string FindingsPath = "/home/u/.agentic-workflow/acme-api/reviews/42.json";
    private static readonly InstanceAddress _address = InstanceAddress.From(A.FirstItemId.Value + "/skill-trial-review/1");
    private static readonly WorkflowInstanceId _instance = WorkflowInstanceId.Of(_address);

    private static string InstanceRoute => "/sindri/workflow/instances/" + Uri.EscapeDataString(_instance.Value);

    private static string FactRoute => InstanceRoute + "/facts/" + Uri.EscapeDataString("fact:reviewFindings");

    private static FactView FactValue(string json) => new("fact:reviewFindings", FactType.Text, JsonDocument.Parse(json).RootElement, Truncated: false);

    /// <summary>An API where the trial starts and its instance has already ended on 'pass', with one produced output whose row carries only the hash; the facts route is left to the test.</summary>
    private static RoutedApi FollowedRun()
    {
        var recipe = new RecipeVersion(RecipeId.From("workflow.skill-trial-review"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
        var view = new InstanceView(_address, recipe, "ended-pass", Detail: null, KnownInstants.Epoch, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null) { Id = _instance };
        var row = new FactRow("fact:reviewFindings", "Findings", FactType.File, FactDirection.Output, From: null, FactStatus.Produced, "sha256:c16c6c90968b", At: null, By: null, Ref: null, Progress: null, Pending: null);
        var ended = new StateCell("ended-pass", Order: null, BranchOf: null, StateGlyph.Done, Badge: null, Round: null, [], Desk: false, Child: false, Children: null, Outcome: null, Terminal: true);
        var detail = WorkflowViewFixtures.Done() with { Address = _address, Id = _instance, State = "ended-pass", EngineOwned = null, States = [ended], Facts = [row] };
        return new RoutedApi().On("/sindri/status", HttpStatusCode.OK, RoutedApi.StatusAtHead7)
            .On("/sindri/items", HttpStatusCode.OK, new ItemCreatedResponse(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 4), A.FirstItemId))
            .On("/sindri/skills/review/runs", HttpStatusCode.OK, new StartSkillRunResponse(view, "review", ManifestHash, ["fact:reviewFindings"], "pass"))
            .On("/sindri/ledger", HttpStatusCode.OK, new LedgerPage([Started(1, """{"launchId":"LCH-1","skill":{"name":"review","resultFile":"/home/u/.agentic-workflow/sindri-app/workflow/runs/WFI-1/review/LCH-1/.sindri-out/result-LCH-1.json"}}""")], Next: null))
            .On(InstanceRoute, HttpStatusCode.OK, detail);
    }

    private static PublicEvent Started(long sequence, string payload) => Event(sequence, "launch.started", payload);

    private static PublicEvent Event(long sequence, string type, string payload) =>
        new(sequence, EventId.From(new Guid((int)sequence, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), type, 1, KnownInstants.Epoch, "service:host", "local", 0, A.FirstItemId,
            CorrelationId.From(new Guid(9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), CausationId: null, TraceId: null, JsonDocument.Parse(payload).RootElement);

    private static ItemView View(long version) =>
        new(A.FirstItemId, "Try the review block", Repo: null, ItemState.New, Step: null, KnownInstants.Epoch, PrimaryAction: null, Actions: [], Owner: null,
            version, "human:desk", WaitingOn: null, Instance: null, KnownInstants.Epoch, KnownInstants.Epoch, Body: null, Link: null, new Core.Launches.ItemLaunch(Modes: [], Core.Launch.AgentId.From("claude-code"), ChosenBy: "builtIn", Attach: null, Resumable: null, InFlight: null, LastStarted: null, Confirm: null, RepoPath: null, Blockers: []),
            new AsOf(5, 5, KnownInstants.Epoch, Rebuilding: false, Stalled: false));

    private SkillRunVerb Verb(RoutedApi api)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new SkillRunVerb(client, new Output(_out, _error, SliceHarness.Json), _ => null, TimeProvider.System);
    }
}
