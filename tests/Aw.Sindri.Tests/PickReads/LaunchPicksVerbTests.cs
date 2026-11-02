using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetLaunchPicks;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PickReads;

/// <summary>'sindri launch picks' (model-pick spec §6.1): a read-only client of GET /sindri/launch/picks that never starts the control plane.</summary>
public sealed class LaunchPicksVerbTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static ModelPicked Picked(string model, string by, string? reason, int calls, int ms, string? from = null) =>
        PickRecords.Picked(A.FirstLaunchId, InstanceAddress.From("T-1/step/1"), model: model, by: by, reason: reason, jevCalls: calls, jevMs: ms, from: from);

    private static LaunchPickView Row(ModelPicked? pick, int escalation = 0, long input = 10_000, long output = 2_000, decimal? cost = 0.02m, bool picking = false) =>
        new(
            Launch: A.FirstLaunchId, Item: A.FirstItemId, Instance: InstanceAddress.From("T-1/step/1"), State: "questions", Attempt: 1, Escalation: escalation, Picking: picking, Pick: pick,
            InputTokens: input, OutputTokens: output, CostUsd: cost, Outcome: LaunchPickOutcomes.Passed, Why: pick is null ? PickWhy.Asking : PickWhy.Sentence(pick), At: KnownInstants.Epoch);

    private static string Body(params LaunchPickView[] rows) => Encoding.UTF8.GetString(AwJson.Serialize(new LaunchPicks(rows), SliceHarness.Json));

    private LaunchPicksVerb Verb(Api api, FakeControlPlaneProbe? probe = null)
    {
        var client = new ControlPlaneClient(probe ?? _up, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new LaunchPicksVerb(client, new Output(_out, _error, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Text_is_one_line_per_pick_with_item_step_agent_model_by_why_tokens_cost_and_jev()
    {
        var api = new Api(HttpStatusCode.OK, Body(Row(Picked("haiku", "jev", reason: null, calls: 1, ms: 2_100))));

        var code = await Verb(api).Run([], TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        var line = Assert.Single(_out.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'));
        Assert.Equal(
            $"{A.FirstItemId.Value}  questions  claude-code  haiku  jev  Picked by Jev: the cheapest model it expects to pass.  12k tokens  $0.02  jev 1 call 2.1 s",
            line);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_retry_ends_its_step_in_e1_and_a_pick_jev_was_not_asked_for_says_so()
    {
        var retried = Row(Picked("sonnet", "rules", reason: "no-api-key", calls: 0, ms: 0, from: "haiku"), escalation: 1);
        var api = new Api(HttpStatusCode.OK, Body(retried));

        _ = await Verb(api).Run([], TestContext.Current.CancellationToken);

        var line = _out.ToString();
        Assert.Contains("  questions.e1  ", line, StringComparison.Ordinal);
        Assert.Contains("  jev not asked", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unpriced_run_says_cost_unknown_and_a_pick_still_being_made_says_choosing()
    {
        var api = new Api(HttpStatusCode.OK, Body(Row(Picked("model-x", "jev", reason: null, calls: 1, ms: 500), cost: null), Row(pick: null, picking: true)));

        _ = await Verb(api).Run([], TestContext.Current.CancellationToken);

        var text = _out.ToString();
        Assert.Contains("cost unknown", text, StringComparison.Ordinal);
        Assert.Contains("choosing model", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_escalation_rate_is_one_line_under_the_picks_and_json_carries_it()
    {
        var rate = new StateEscalation("questions", "haiku", Runs: 20, Retried: 3, EscalationRate: 0.15);
        var body = Encoding.UTF8.GetString(AwJson.Serialize(new LaunchPicks([Row(Picked("haiku", "jev", reason: null, calls: 1, ms: 100))], [rate]), SliceHarness.Json));

        _ = await Verb(new Api(HttpStatusCode.OK, body)).Run([], TestContext.Current.CancellationToken);
        var lines = _out.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

        Assert.Equal("questions  haiku  retried on a stronger model in 3 of 20 runs", lines[^1]);
        Assert.Contains("\"escalation\":[{\"state\":\"questions\",\"model\":\"haiku\",\"runs\":20,\"retried\":3", body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_picks_says_so()
    {
        _ = await Verb(new Api(HttpStatusCode.OK, Body())).Run([], TestContext.Current.CancellationToken);

        Assert.Equal("no picks\n", _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Json_prints_the_routes_answer()
    {
        var body = Body(Row(Picked("haiku", "jev", reason: null, calls: 1, ms: 100)));

        var code = await Verb(new Api(HttpStatusCode.OK, body)).Run(["--json"], TestContext.Current.CancellationToken);

        Assert.Equal((0, body + "\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_item_flag_is_passed_to_the_route()
    {
        var api = new Api(HttpStatusCode.OK, Body());

        _ = await Verb(api).Run(["--item", "ENG-142"], TestContext.Current.CancellationToken);

        Assert.Equal("?item=ENG-142", api.Query);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--wat")]
    [InlineData("--item")]
    public async Task A_bad_argument_is_refused_without_calling(string flag)
    {
        var api = new Api(HttpStatusCode.OK, Body());

        var code = await Verb(api).Run([flag], TestContext.Current.CancellationToken);

        Assert.Equal((2, 0), (code, api.Calls));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_the_control_plane_is_down_it_exits_1_with_SND_API_001_and_starts_nothing()
    {
        var api = new Api(HttpStatusCode.OK, Body());

        var code = await Verb(api, new FakeControlPlaneProbe()).Run([], TestContext.Current.CancellationToken);

        Assert.Equal((1, 0), (code, api.Calls));
        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Query { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Query = request.RequestUri?.Query;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
