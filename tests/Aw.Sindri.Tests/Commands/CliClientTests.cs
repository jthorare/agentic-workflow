using System.Net;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The CLI's client over a canned API: the caller headers it sends, the raw body --json prints, a 403 and its refusal text.</summary>
public sealed class CliClientTests : IDisposable
{
    private const string Refusal = "StopProcess is a human decision. Ask the human to run it in a terminal or the Sindri app: sindri stop";
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
    public async Task Json_prints_the_api_body_unchanged()
    {
        var h = new SliceHarness();
        var status = (await new GetStatusService(h.Held, new ProfileReads(h.Profiles, h.Profiles), h.Store, h.Events, FirstPass.Progress(h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot()).Handle(new GetStatusRequest(), Token)).Value;
        var body = "{ " + Encoding.UTF8.GetString(AwJson.Serialize(status, SliceHarness.Json))[1..].Replace(",", ",\n  ", StringComparison.Ordinal);   // spaced as no serializer here would write it
        using var client = Client(new CliStubApi(HttpStatusCode.OK, body));

        var code = await Verbs(client).Status(json: true, Token);

        Assert.Equal((1, body + "\n"), (code, _out.ToString()));   // exit 1: this profile needs approval
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_403_is_read_as_forbidden()
    {
        using var client = Client(new CliStubApi(HttpStatusCode.Forbidden, Problem("SND-AUTH-002", Refusal)));

        var stopped = await client.Post<StopProcessRequest, StopProcessResponse>("/sindri/stop", body: null, Token);

        Assert.Equal(("SND-AUTH-002", ErrorKind.Forbidden), (stopped.Error.Code, stopped.Error.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Refused_command_names_why_the_cli_ran_as_a_session()
    {
        using var client = Client(new CliStubApi(HttpStatusCode.Forbidden, Problem("SND-AUTH-002", Refusal)), new AgentSession(Terminal: true, Prover: false), human: false);

        var code = await Verbs(client).Stop(Token);

        Assert.Equal(2, code);
        Assert.Equal($"SND-AUTH-002 {Refusal} (this CLI ran as a session: the per-start human token isn't readable)\n", _error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refusal_of_a_human_cli_carries_no_session_note()
    {
        using var client = Client(new CliStubApi(HttpStatusCode.Forbidden, Problem("SND-AUTH-001", "A human caller needs the per-start token")),
            new AgentSession(Terminal: true, Prover: false));

        var code = await Verbs(client).Stop(Token);

        Assert.Equal((2, "SND-AUTH-001 A human caller needs the per-start token\n"), (code, _error.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_api_call_carries_the_caller_headers()
    {
        var api = new CliStubApi(HttpStatusCode.Forbidden, Problem("SND-AUTH-002", Refusal));
        using var client = Client(api, new AgentSession(Terminal: true, Prover: false), human: false, repo: "demo");

        _ = await Verbs(client).Stop(Token);

        var (route, headers) = Assert.Single(api.Calls);
        Assert.Equal(("/sindri/stop", "session", "cli", "demo"), (route, headers[SindriHeaders.Caller], headers[SindriHeaders.Client], headers[SindriHeaders.Repo]));
    }

    private static string Problem(string code, string detail) => $$"""{"type":"about:blank","title":"{{code}}","status":403,"detail":"{{detail}}"}""";

    private static ControlPlaneClient Client(CliStubApi api, AgentSession? session = null, bool human = true, string? repo = null) =>
        new(_probe, SliceHarness.Json,
            session is null ? null : CallerHeaders.For(session, human ? InMemoryHumanTokenStore.WithToken() : new InMemoryHumanTokenStore(), _ => ValueTask.FromResult(repo is null ? (RepoName?)null : RepoName.From(repo))),
            handler: api);

    private Verbs Verbs(ControlPlaneClient client) =>
        new(client, _probe, new Output(_out, _error, SliceHarness.Json), new ServiceCollection().BuildServiceProvider());
}
