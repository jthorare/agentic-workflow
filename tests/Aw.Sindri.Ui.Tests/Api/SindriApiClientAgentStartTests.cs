using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's start route (agents spec §15.8, A7): POST /sindri/sessions with the form's body, the Starting session read back, a refusal with its code, words and sign-in command, and the last choices.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientAgentStartTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private const string Started = """{"id":"sindri-act00000001","actId":"act00000001","state":"starting","host":{"socket":"/tmp/tmux-1000/sindri-launch","session":"sindri-act00000001","pane":"%4"}}""";
    private const string NotSignedIn = """{"type":"about:blank","title":"SND-AGENT-013","status":400,"detail":"Codex is not signed in (fix: codex login)","command":"codex login"}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(Canned handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public async Task Start_posts_the_form_body_to_sessions_as_the_human_and_reads_the_starting_session()
    {
        var canned = new Canned(HttpStatusCode.OK, Started);

        var started = await Client(canned).Start(_api, new StartAgentRequest("sindri", "claude-code", "-fix it", Worktree: true, "act00000001"), Token);

        using var body = JsonDocument.Parse(canned.Bodies[0]);
        var sent = body.RootElement;
        Assert.Equal((HttpMethod.Post, "/sindri/sessions"), (canned.Methods[0], canned.Seen[0].AbsolutePath));
        Assert.Equal(("sindri", "claude-code", "-fix it", true, "act00000001"), (sent.GetProperty("repo").GetString(), sent.GetProperty("agent").GetString(), sent.GetProperty("prompt").GetString(), sent.GetProperty("worktree").GetBoolean(), sent.GetProperty("actId").GetString()));
        Assert.Equal(new AgentStartedView("sindri-act00000001", "act00000001", "starting", new AgentStartedHost("/tmp/tmux-1000/sindri-launch", "sindri-act00000001", "%4")), started.Value);
    }

    [Fact]
    public async Task A_refusal_keeps_its_code_its_words_and_the_sign_in_command_extension()
    {
        var canned = new Canned(HttpStatusCode.BadRequest, NotSignedIn);

        var refused = await Client(canned).Start(_api, new StartAgentRequest("sindri", "codex", Prompt: null, Worktree: false, "act00000002"), Token);

        var command = refused.Error.Extensions!.Single(e => e.Key == "command").Value.GetString();
        Assert.Equal(("SND-AGENT-013", "Codex is not signed in (fix: codex login)", "codex login"), (refused.Error.Code, refused.Error.Message, command));
    }

    [Fact]
    public async Task LastStart_reads_the_repo_and_agent_of_the_last_start()
    {
        var canned = new Canned(HttpStatusCode.OK, """{"repo":"ledgerly","agent":"codex"}""");

        var last = await Client(canned).LastStart(_api, Token);

        Assert.Equal(("/sindri/sessions/last-start", new AgentLastStartView("ledgerly", "codex")), (canned.Seen[0].AbsolutePath, last.Value));
    }

    [Fact]
    public async Task The_fake_records_the_start_and_answers_a_starting_session_with_the_same_shape()
    {
        var fake = new FakeSindriApi();

        var started = await fake.Start(_api, new StartAgentRequest("sindri", "codex", Prompt: null, Worktree: false, "act00000003"), Token);

        Assert.Equal((["act00000003"], "starting", "sindri-act00000003"), (fake.Starts.Select(s => s.ActId).ToList(), started.Value.State, started.Value.Id));
    }

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Uri> Seen { get; } = [];

        public List<HttpMethod> Methods { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request.RequestUri!); // why: HttpClient always sets the URI
            Methods.Add(request.Method);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
