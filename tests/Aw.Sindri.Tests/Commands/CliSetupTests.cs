using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri setup [--json]': a client of GET /sindri/setup that only reports, never starts the control plane.</summary>
public sealed class CliSetupTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly AsOf _asOf = new(Cursor: 5, LedgerHead: 5, ProjectedAt: new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero), Rebuilding: false, Stalled: false);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // the deriver fills Cli from the action (SetupDeriver.Derive); the raw SetupSteps.* here get the same, so the exit-3 rule sees what a served list carries
    private static string Body(params SetupStep[] steps) => Encoding.UTF8.GetString(AwJson.Serialize(new GetSetupResponse([.. steps.Select(static s => s with { Cli = SetupActions.CliFor(s.Action) })], _asOf), SliceHarness.Json));

    private SetupVerbs Verbs(FakeControlPlaneProbe probe, Api api) =>
        new(new ControlPlaneClient(probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api),
            new Output(_out, _error, SliceHarness.Json));

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_exits_1_without_calling_anything_when_serve_is_down()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(new FakeControlPlaneProbe(), api).Run([], Token);

        Assert.Equal((1, 0), (code, api.Calls));
        Assert.StartsWith("SND-API-001 ", _error.ToString(), StringComparison.Ordinal);
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
