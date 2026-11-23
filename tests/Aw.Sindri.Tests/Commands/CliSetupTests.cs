using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.SetupSources;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri setup [--json]': a client of GET /sindri/setup that only reports, never starts the control plane.</summary>
public sealed class CliSetupTests : IAsyncDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly AsOf _asOf = new(Cursor: 5, LedgerHead: 5, ProjectedAt: new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero), Rebuilding: false, Stalled: false);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly SetupFactsRig _rig = new();

    public async ValueTask DisposeAsync()
    {
        await _out.DisposeAsync();
        await _error.DisposeAsync();
        await _rig.DisposeAsync();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // the deriver fills Cli from the action (SetupDeriver.Derive); the raw SetupSteps.* here get the same, so the exit-3 rule sees what a served list carries
    private static string Body(params SetupStep[] steps) => Encoding.UTF8.GetString(AwJson.Serialize(new GetSetupResponse([.. steps.Select(static s => s with { Cli = SetupActions.CliFor(s.Action) })], _asOf), SliceHarness.Json));

    private SetupVerbs Verbs(FakeControlPlaneProbe probe, Api api) =>
        new(new ControlPlaneClient(probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api),
            new Output(_out, _error, SliceHarness.Json), new GetSetupOfflineService(_rig.Facts));

    // first-run spec decision 8: with serve down the CLI decides serve, repo, profile and agent in process and says the other four wait for Sindri to run
    [Fact, Trait("Lane", "unit")]
    public async Task Setup_with_serve_down_reports_not_running_first()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(new FakeControlPlaneProbe(), api).Run([], Token);

        Assert.Equal((1, 0), (code, api.Calls));
        Assert.StartsWith("SND-API-001 ", _error.ToString(), StringComparison.Ordinal);
        var lines = _out.ToString().ReplaceLineEndings("\n").Split('\n');
        Assert.Equal("todo      Sindri is current: Sindri isn't running.", lines[0]);
        string[] rest =
            [
                "todo      Connect your accounts: Waits for Sindri to run.",
                "needs-you Add your repo: Pick the folder of a repo to start.",
                "todo      Save the profile: Waits for your first repo.",
                "todo      Agent for workflows: Waits for your first repo.",
                "todo      Connect your agents: Waits for Sindri to run.",
                "todo      Build the map: Waits for Sindri to run.",
                "todo      Run hello-sindri: Waits for Sindri to run.",
                "0 of 8 steps done.",
            ];
        Assert.Equal(rest, lines[1..^1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_with_serve_down_decides_repo_profile_and_agent_from_the_profile()
    {
        _rig.Profiles.Live = SetupFactsRig.ProfileWithRepos();

        var code = await Verbs(new FakeControlPlaneProbe(), new Api(HttpStatusCode.OK, "{}")).Run([], Token);

        var lines = _out.ToString().ReplaceLineEndings("\n").Split('\n');
        Assert.Equal(1, code);   // serve is still to start
        string[] decided = ["done      Add your repo: demo", "done      Save the profile: Your profile is saved and live.", "todo      Agent for workflows: Sindri picks the first agent it finds."];
        Assert.Equal(decided, lines[2..5]);
        Assert.Equal("2 of 8 steps done.", lines[^2]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_json_with_serve_down_keeps_the_routes_shape()
    {
        var code = await Verbs(new FakeControlPlaneProbe(), new Api(HttpStatusCode.OK, "{}")).Run(["--json"], Token);

        Assert.Equal(1, code);
        var answer = AwJson.Deserialize<GetSetupResponse>(Encoding.UTF8.GetBytes(_out.ToString()), SliceHarness.Json).Value;
        SetupStepId[] order = [SetupStepId.Serve, SetupStepId.Sources, SetupStepId.Repo, SetupStepId.Profile, SetupStepId.Agent, SetupStepId.Mcp, SetupStepId.Map, SetupStepId.FirstWorkflow];
        Assert.Equal(order, answer.Steps.Select(static s => s.Id));
        Assert.Equal(0, answer.AsOf.Cursor);   // nothing was projected
        Assert.Null(answer.AsOf.ProjectedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_prints_a_line_per_step_with_its_plain_title_state_and_detail_and_exits_1_while_one_is_open()
    {
        var api = new Api(HttpStatusCode.OK, Body(
            SetupSteps.Repo(new RepoFact(1)),
            SetupSteps.Profile(new ProfileFact(ProfilePresence.Present), new RepoFact(1)),
            SetupSteps.Map(new MapFact(MapPhase.Failed, FailureCode: "SND-CONTEXT-040"))));

        var code = await Verbs(_up, api).Run([], Token);

        Assert.Equal((1, "/sindri/setup"), (code, api.Path));
        Assert.Equal(
            "done      Add your repo: 1 repository\n"
            + "done      Save the profile: Your profile is saved and live.\n"
            + "failed    Build the map: The last try at building the map didn't finish.\n"
            + "2 of 3 steps done.\n",
            _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_exits_0_when_every_step_is_done_or_skipped_and_says_why_and_what_to_do()
    {
        var api = new Api(HttpStatusCode.OK, Body(SetupSteps.Repo(new RepoFact(1)), new SetupStep(SetupStepId.Sources, "Connect your sources", SetupState.Skipped, "Nothing to connect here.", "Connect a source any time.", Error: null, Action: null, Progress: null)));

        var code = await Verbs(_up, api).Run([], Token);

        Assert.Equal(0, code);
        Assert.Contains("skipped   Connect your sources: Nothing to connect here. Connect a source any time.\n", _out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_exits_3_when_every_open_step_waits_on_the_person_at_the_keyboard()
    {
        var api = new Api(HttpStatusCode.OK, Body(
            SetupSteps.Repo(new RepoFact(1)),
            new SetupStep(SetupStepId.Sources, "Connect your accounts", SetupState.NeedsYou, "Sign in to GitHub in your browser.", Detail: null, Error: null, "ConnectAccount", Progress: null)));   // Cli is null: the action is the owner's

        var code = await Verbs(_up, api).Run([], Token);

        Assert.Equal(3, code);   // first-run spec decision 9: an agent stops here and tells the owner
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_exits_1_when_an_open_step_has_a_command_an_agent_can_run()
    {
        var api = new Api(HttpStatusCode.OK, Body(
            new SetupStep(SetupStepId.Sources, "Connect your accounts", SetupState.NeedsYou, "Sign in to GitHub in your browser.", Detail: null, Error: null, "ConnectAccount", Progress: null),
            new SetupStep(SetupStepId.Mcp, "Agents can use Sindri's tools", SetupState.Todo, "Sindri is not registered in every repo yet.", Detail: null, Error: null, "RegisterMcp", Progress: null) { Cli = "sindri mcp register --apply" }));

        var code = await Verbs(_up, api).Run([], Token);

        Assert.Equal(1, code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_exits_0_when_every_step_is_done()
    {
        var api = new Api(HttpStatusCode.OK, Body(SetupSteps.Repo(new RepoFact(1)), SetupSteps.Profile(new ProfileFact(ProfilePresence.Present), new RepoFact(1))));

        var code = await Verbs(_up, api).Run([], Token);

        Assert.Equal((0, "2 of 2 steps done."), (code, _out.ToString().ReplaceLineEndings("\n").Split('\n')[^2]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_json_prints_the_routes_body_as_served()
    {
        var body = Body(SetupSteps.Repo(new RepoFact(0)));

        var code = await Verbs(_up, new Api(HttpStatusCode.OK, body)).Run(["--json"], Token);

        Assert.Equal((1, body), (code, _out.ToString().Trim()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_refuses_an_argument_it_does_not_know_before_calling_anything()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(_up, api).Run(["--repair"], Token);

        Assert.Equal((2, 0), (code, api.Calls));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("accounts", "sources")]
    [InlineData("first-workflow", "first-workflow")]
    public async Task Setup_skip_posts_the_step_and_says_the_undo(string word, string id)
    {
        var api = new Api(HttpStatusCode.OK, $$"""{"step":"{{id}}","skipped":true,"changed":true}""");

        var code = await Verbs(_up, api).Run(["skip", word], Token);

        Assert.Equal((0, "POST", "/sindri/setup/skip"), (code, api.Method, api.Path));
        Assert.Contains($$"""{"step":"{{id}}","skipped":true}""", api.Sent, StringComparison.Ordinal);
        Assert.Contains($"sindri setup skip {word} --undo", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_skip_with_undo_posts_skipped_false()
    {
        var api = new Api(HttpStatusCode.OK, """{"step":"sources","skipped":false,"changed":true}""");

        var code = await Verbs(_up, api).Run(["skip", "accounts", "--undo"], Token);

        Assert.Equal(0, code);
        Assert.Contains("\"skipped\":false", api.Sent, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("repo")]
    [InlineData("")]
    public async Task Setup_skip_of_a_step_that_may_not_be_skipped_is_a_usage_error_and_calls_nothing(string word)
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(_up, api).Run(word.Length == 0 ? ["skip"] : ["skip", word], Token);

        Assert.Equal((2, 0), (code, api.Calls));
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Path { get; private set; }

        public string? Method { get; private set; }

        public string? Sent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Path = request.RequestUri?.AbsolutePath;
            Method = request.Method.Method;
            Sent = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
