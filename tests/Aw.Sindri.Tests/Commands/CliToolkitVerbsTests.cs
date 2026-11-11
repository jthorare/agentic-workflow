using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.ListToolkitCalls;
using Aw.Sindri.Features.ListToolkits;
using Aw.Sindri.Features.PauseToolkit;
using Aw.Sindri.Features.ResumeToolkit;
using Aw.Sindri.Features.StartToolkit;
using Aw.Sindri.Features.StopToolkit;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri toolkit list|create|build|start|stop|pause|resume|calls' (agent-first PR 16c) over a routed API: the route each verb calls, what it prints, a refused call and bad usage.</summary>
[Lane(TestLane.Unit)]
public sealed class CliToolkitVerbsTests : IDisposable
{
    private const string Started = """{"accepted":{"commandId":"00000000-0000-0000-0000-00000000000c","sequence":5},"jobId":"JOB-0000000000000000000000000000000e","links":{"job":"/sindri/jobs/JOB-0000000000000000000000000000000e","cancel":"/sindri/jobs/JOB-0000000000000000000000000000000e/cancel","events":"/sindri/events?after=4&types=job.*"}}""";

    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Text => _out.ToString().ReplaceLineEndings("\n");

    private async Task<int> Run(RoutedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        var output = new Output(_out, _error, SliceHarness.Json);
        return await new ToolkitVerbs(client, output, new JobFollower(client, output, TimeProvider.System)).Run(["toolkit", .. args], Token, Token);
    }

    [Fact]
    public async Task List_reads_the_toolkits_route_and_prints_a_line_per_toolkit()
    {
        var manifest = ToolkitManifest.Parse("""{"name":"notes","version":"1.0.0","resources":[],"tools":[{"name":"add_note","description":"d","inputSchema":{"type":"object"},"outputSchema":{"type":"object"}}]}""").Value;
        var api = new RoutedApi().On("/sindri/toolkits", HttpStatusCode.OK, new ListToolkitsResponse([
            new Toolkit("notes", "/tk/notes", ToolkitState.Running, SourceHash: "h", BuiltAt: DateTimeOffset.UnixEpoch, Manifest: manifest, CreatedBy: "owner"),
            new Toolkit("scratch", "/tk/scratch", ToolkitState.Off, SourceHash: null, BuiltAt: null, Manifest: null, CreatedBy: "adopted")]));

        var exit = await Run(api, "list");

        Assert.Equal(0, exit);
        Assert.Equal("notes  running  add_note\nscratch  off\n", Text);
        Assert.Equal(("GET", "/sindri/toolkits"), (api.Calls[^1].Method, api.Calls[^1].Path));
    }

    [Theory]
    [InlineData("start", "running")]
    [InlineData("stop", "built")]
    [InlineData("pause", "paused")]
    [InlineData("resume", "running")]
    public async Task A_state_verb_posts_to_its_route_and_prints_the_state(string verb, string state)
    {
        var api = new RoutedApi();
        _ = verb switch
        {
            "start" => api.OnPost("/sindri/toolkits/notes/start", HttpStatusCode.OK, new StartToolkitResponse("notes", ToolkitState.Running)),
            "stop" => api.OnPost("/sindri/toolkits/notes/stop", HttpStatusCode.OK, new StopToolkitResponse("notes", ToolkitState.Built)),
            "pause" => api.OnPost("/sindri/toolkits/notes/pause", HttpStatusCode.OK, new PauseToolkitResponse("notes", ToolkitState.Paused)),
            _ => api.OnPost("/sindri/toolkits/notes/resume", HttpStatusCode.OK, new ResumeToolkitResponse("notes", ToolkitState.Running)),
        };

        var exit = await Run(api, verb, "notes");

        Assert.Equal(0, exit);
        Assert.Equal($"notes {state}\n", Text);
        Assert.Equal(("POST", $"/sindri/toolkits/notes/{verb}"), (api.Calls[^1].Method, api.Calls[^1].Path));
    }

    [Fact]
    public async Task A_refused_pause_prints_the_error_and_exits_2_like_every_refused_sindri_verb()
    {
        var api = new RoutedApi().OnRaw("/sindri/toolkits/notes/pause", HttpStatusCode.Conflict, """{"type":"about:blank","title":"SND-TK-001","status":409,"detail":"Toolkit 'notes' isn't running (fix: toolkit_start)"}""");

        var exit = await Run(api, "pause", "notes");

        Assert.Equal(2, exit);
        Assert.Contains("SND-TK-001", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_posts_the_name_tool_and_needs_and_with_detach_prints_the_job()
    {
        var api = new RoutedApi().OnRaw("/sindri/toolkits", HttpStatusCode.OK, Started);

        var exit = await Run(api, "create", "github-triage", "--tool", "list_issues", "--needs", "connection:github", "--detach");

        Assert.Equal(0, exit);
        var call = api.Calls.Single(static c => c.Method == "POST");
        Assert.Equal("/sindri/toolkits", call.Path);
        Assert.Contains("\"name\":\"github-triage\"", call.Body, StringComparison.Ordinal);
        Assert.Contains("\"tool\":\"list_issues\"", call.Body, StringComparison.Ordinal);
        Assert.Contains("\"needs\":\"connection:github\"", call.Body, StringComparison.Ordinal);
        Assert.Contains("job JOB-0000000000000000000000000000000e started", Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_posts_the_name_to_the_build_route_and_with_detach_prints_the_job()
    {
        var api = new RoutedApi().OnRaw("/sindri/toolkits/build", HttpStatusCode.OK, Started);

        var exit = await Run(api, "build", "notes", "--detach");

        Assert.Equal(0, exit);
        var call = api.Calls.Single(static c => c.Method == "POST");
        Assert.Equal(("/sindri/toolkits/build", "{\"name\":\"notes\"}"), (call.Path, call.Body));
        Assert.Contains("job JOB-0000000000000000000000000000000e started", Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Calls_reads_the_calls_route_with_the_limit_and_prints_tool_caller_duration_and_error()
    {
        var at = new DateTimeOffset(2026, 11, 8, 9, 30, 0, TimeSpan.Zero);
        var api = new RoutedApi().On("/sindri/toolkits/notes/calls", HttpStatusCode.OK, new ListToolkitCallsResponse([
            new ToolkitCall("notes", "add_note", "session:abc", Input: "{}", Output: "{}", Truncated: false, Error: null, DurationMs: 12, At: at),
            new ToolkitCall("notes", "add_note", "workflow:run-1", Input: "{}", Output: null, Truncated: false, Error: "SND-TK-011 boom", DurationMs: 3, At: at)]));

        var exit = await Run(api, "calls", "notes", "--limit", "5");

        Assert.Equal(0, exit);
        Assert.Equal("?limit=5", api.Calls[^1].Query);
        Assert.Equal("2026-11-08 09:30:00Z  add_note  session:abc  12 ms\n2026-11-08 09:30:00Z  add_note  workflow:run-1  3 ms  SND-TK-011 boom\n", Text);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("pause", "a", "b")]
    [InlineData("create", "notes")]
    [InlineData("calls", "notes", "--limit", "x")]
    [InlineData("frobnicate", "notes")]
    [InlineData]
    public async Task Bad_usage_is_refused_without_a_call(params string[] args)
    {
        var api = new RoutedApi();

        var exit = await Run(api, args);

        Assert.NotEqual(0, exit);
        Assert.DoesNotContain(api.Calls, static c => c.Path.StartsWith("/sindri/toolkits", StringComparison.Ordinal));
    }
}
