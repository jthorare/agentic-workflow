using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.ListSessions;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri agents' and 'sindri agents show' (collector spec CI-8): read the routes, never start the server.</summary>
public sealed class CliAgentsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SessionList ListOf(IReadOnlyList<SessionRow> rows, IReadOnlyList<SessionProblem>? problems = null) =>
        new(rows, [], problems ?? [], new SessionCounts(rows.Count, rows.Count(r => r.State == SessionState.Asking), rows.Count), rows.Count == 0 ? "No agent sessions are running." : null, SessionData.T0);

    private static SessionRow Row(ObservedSession s, string label) => new($"{s.Ref}", s.Ref.Provider, label, s.Ref.Id, s.IdKnown, s.State, s.Reason is { } r ? new SessionAsking(r, s.Tool, s.Since) : null,
        StateSource.Hook, Estimated: false, s.Repo, Worktree: null, new SessionHostRow(s.Host.Kind, s.Host.Label, SocketKind: null, AttachedClients: null, Terminal: null), Lane: null, s.LaunchId, s.StartedAt, s.Since, SessionFocus.Pending);

    private static string Body<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    private AgentsVerbs Verbs(FakeControlPlaneProbe probe, Api api) =>
        new(new ControlPlaneClient(probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api),
            new Output(_out, _error, SliceHarness.Json));

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_prints_sandboxed_and_the_memory_in_use_against_the_limit_on_a_sandboxed_row()
    {
        var step = Row(SessionsRig.Claude(), "Claude Code") with { Sandbox = SandboxData.Reading() };
        var attended = Row(SessionsRig.Codex(), "Codex") with { Sandbox = SandboxData.Reading(attended: true) };
        var api = new Api(HttpStatusCode.OK, Body(ListOf([step, attended])));

        _ = await Verbs(_up, api).Run([], Token);

        var lines = _out.ToString().ReplaceLineEndings("\n").Split('\n');
        Assert.EndsWith("  sandboxed · mem 2.1/12 GiB", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("  sandboxed · attended · mem 2.1/12 GiB", lines[2], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_prints_details_unavailable_for_a_sandbox_whose_plan_does_not_read()
    {
        var row = Row(SessionsRig.Claude(), "Claude Code") with { Sandbox = new SessionSandbox(SessionSandboxState.Unavailable, Attended: false, Folder: null, Memory: null) };
        var api = new Api(HttpStatusCode.OK, Body(ListOf([row])));

        _ = await Verbs(_up, api).Run([], Token);

        Assert.Contains("  sandboxed · details unavailable", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_prints_nothing_about_the_sandbox_on_a_row_without_one()
    {
        var api = new Api(HttpStatusCode.OK, Body(ListOf([Row(SessionsRig.Claude(), "Claude Code")])));

        _ = await Verbs(_up, api).Run([], Token);

        Assert.DoesNotContain("sandboxed", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_cli_exits_1_when_serve_is_down()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(new FakeControlPlaneProbe(), api).Run([], Token);

        Assert.Equal((1, 0), (code, api.Calls));
        Assert.StartsWith("SND-API-001 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_show_exits_1_when_serve_is_down()
    {
        var code = await Verbs(new FakeControlPlaneProbe(), new Api(HttpStatusCode.OK, "{}")).Run(["show", "claude-code/abc"], Token);

        Assert.Equal(1, code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_prints_a_head_and_a_line_per_session_with_the_asking_reason_and_tool()
    {
        var api = new Api(HttpStatusCode.OK, Body(ListOf([Row(SessionsRig.Claude(), "Claude Code"), Row(SessionsRig.Codex(), "Codex")])));

        var code = await Verbs(_up, api).Run([], Token);

        Assert.Equal(0, code);
        var text = _out.ToString().ReplaceLineEndings("\n");
        Assert.Contains("2 live · 1 asking · 2 outside a workflow", text, StringComparison.Ordinal);
        Assert.Contains($"codex/{SessionsRig.CodexId}  ASKING · approval · Bash  web  Warp", text, StringComparison.Ordinal);
        Assert.Contains($"claude-code/{SessionsRig.ClaudeId}  RUNNING  api  tmux · Warp", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_prints_the_empty_sentence_and_exits_0_for_an_empty_list()
    {
        var code = await Verbs(_up, new Api(HttpStatusCode.OK, Body(ListOf([])))).Run([], Token);

        Assert.Equal((0, "No agent sessions are running.\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_exits_1_when_a_problem_row_is_present_and_prints_it()
    {
        var problem = new SessionProblem("claude-records", "SND-SESSION-001", "The claude-records source couldn't be read in the last pass");

        var code = await Verbs(_up, new Api(HttpStatusCode.OK, Body(ListOf([Row(SessionsRig.Claude(), "Claude Code")], [problem])))).Run([], Token);

        Assert.Equal(1, code);
        Assert.Contains("SND-SESSION-001", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_json_is_the_raw_body_and_the_filters_become_the_query()
    {
        var body = Body(ListOf([]));
        var api = new Api(HttpStatusCode.OK, body);

        var code = await Verbs(_up, api).Run(["--provider", "codex", "--state", "asking", "--repo", "web", "--json"], Token);

        Assert.Equal((0, body + "\n", "?provider=codex&state=asking&repo=web"), (code, _out.ToString().ReplaceLineEndings("\n"), api.Query));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--wat")]
    [InlineData("--provider")]
    [InlineData("show")]
    public async Task Agents_refuses_a_bad_argument_without_calling(string argument)
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(_up, api).Run([argument], Token);

        Assert.Equal((2, 0), (code, api.Calls));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_show_prints_the_session_and_asks_the_route_for_its_ref()
    {
        var row = Row(SessionsRig.Claude(), "Claude Code") with { Title = "t", Pid = 5 };
        var api = new Api(HttpStatusCode.OK, Body(new SessionView(row, "/work/api", [new SessionStateEntry(SessionState.Running, Reason: null, SessionData.T0, StateSource.Hook)], SessionData.T0)));

        var code = await Verbs(_up, api).Run(["show", $"claude-code/{SessionsRig.ClaudeId}"], Token);

        Assert.Equal((0, $"/sindri/sessions/claude-code/{SessionsRig.ClaudeId}"), (code, api.Path));
        var text = _out.ToString();
        Assert.Contains("/work/api", text, StringComparison.Ordinal);
        Assert.Contains("running", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agents_show_reports_the_404_with_exit_2()
    {
        var api = new Api(HttpStatusCode.NotFound, """{"title":"SND-SESSION-404","status":404,"detail":"No such live session"}""");

        var code = await Verbs(_up, api).Run(["show", "codex/nope"], Token);

        Assert.Equal(2, code);
        Assert.Contains("SND-SESSION-404", _error.ToString(), StringComparison.Ordinal);
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Query { get; private set; }

        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Query = request.RequestUri?.Query;
            Path = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, (int)status >= 400 ? "application/problem+json" : "application/json") });
        }
    }
}
