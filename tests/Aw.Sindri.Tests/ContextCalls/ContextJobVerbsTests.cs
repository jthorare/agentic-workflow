using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'context sources sync' and 'context map build' over a scripted API (API P6 follow-up): the start each posts, what it prints with --detach and --json, and a refused start.</summary>
public sealed class ContextJobVerbsTests : IDisposable
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

    private CancellationToken _interrupt = Token;

    private async Task<int> Run(ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        var output = new Output(_out, _error, SliceHarness.Json);
        return await new ContextJobVerbs(client, output, new JobFollower(client, output, TimeProvider.System)).Run(args, _interrupt, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sources_sync_posts_the_start_and_with_detach_prints_the_job_and_returns()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));

        var exit = await Run(api, "sources", "sync", "--detach");

        Assert.True(exit == 0, _error.ToString());
        var (method, route, _) = Assert.Single(api.Calls);
        Assert.Equal(("POST", "/sindri/context/sources/sync"), (method, route));
        Assert.Equal("job JOB-0000000000000000000000000000000e started\n", _out.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("sources", "sync")]
    [InlineData("map", "build")]
    public async Task A_Ctrl_C_during_the_start_cancels_the_POST_so_a_hung_start_can_be_left(params string[] args)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));
        using var interrupted = new CancellationTokenSource();
        await interrupted.CancelAsync();

        _interrupt = interrupted.Token;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(api, [.. args, "--detach"]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sources_sync_names_one_source_in_the_body()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));

        _ = await Run(api, "sources", "sync", "slack", "--detach");

        Assert.Contains("\"source\":\"slack\"", Assert.Single(api.Calls).Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_build_posts_to_its_route_and_with_json_and_detach_prints_the_JobStarted_body()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));

        var exit = await Run(api, "map", "build", "--detach", "--json");

        Assert.True(exit == 0, _error.ToString());
        Assert.Equal("/sindri/context/map/build", Assert.Single(api.Calls).Route);
        Assert.Contains("\"jobId\":\"JOB-0000000000000000000000000000000e\"", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_backtest_posts_the_arms_and_window_to_its_route_and_with_detach_prints_the_job()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));

        var exit = await Run(api, "eval", "backtest", "--arms", "brief,graph", "--since", "2026-07-01", "--until", "2026-09-30", "--limit", "10", "--detach");

        Assert.True(exit == 0, _error.ToString());
        var (method, route, body) = Assert.Single(api.Calls);
        Assert.Equal(("POST", "/sindri/context/eval/backtest"), (method, route));
        Assert.Contains("\"arms\":\"brief,graph\"", body, StringComparison.Ordinal);
        Assert.Contains("\"since\":\"2026-07-01T00:00:00", body, StringComparison.Ordinal);
        Assert.Contains("\"until\":\"2026-09-30T23:59:59.9999999", body, StringComparison.Ordinal);   // a date-only --until is the whole day
        Assert.Contains("\"limit\":10", body, StringComparison.Ordinal);
        Assert.Equal("job JOB-0000000000000000000000000000000e started\n", _out.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_backtest_keeps_a_since_date_at_midnight_and_an_until_instant_as_given()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));

        _ = await Run(api, "eval", "backtest", "--since", "2026-07-01", "--until", "2026-09-30T10:00:00Z", "--detach");

        var body = Assert.Single(api.Calls).Body;
        Assert.Contains("\"since\":\"2026-07-01T00:00:00", body, StringComparison.Ordinal);
        Assert.Contains("\"until\":\"2026-09-30T10:00:00", body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_backtest_unknown_arm_082_is_attention_and_exits_1_as_the_error_table_says()
    {
        var api = new ScriptedApi((HttpStatusCode.BadRequest, """{"type":"x","title":"SND-CONTEXT-082","status":400,"detail":"unknown arm 'bogus'"}"""));

        var exit = await Run(api, "eval", "backtest", "--arms", "bogus", "--detach");

        Assert.Equal(1, exit);
        Assert.Contains("SND-CONTEXT-082", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_backtest_with_json_and_detach_prints_the_JobStarted_body_and_a_second_start_is_SND_JOB_409()
    {
        var started = await Run(new ScriptedApi((HttpStatusCode.OK, Started)), "eval", "backtest", "--detach", "--json");
        var conflict = await Run(new ScriptedApi((HttpStatusCode.Conflict,
            """{"type":"x","title":"SND-JOB-409","status":409,"detail":"A RunContextBacktest job is running (JOB-0000000000000000000000000000000e)","runningJob":"JOB-0000000000000000000000000000000e"}""")), "eval", "backtest", "--detach");

        Assert.Equal((0, 2), (started, conflict));
        Assert.Contains("\"jobId\":\"JOB-0000000000000000000000000000000e\"", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("SND-JOB-409", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("eval", "backtest", "--limit", "many")]
    [InlineData("eval", "backtest", "--since", "yesterday-ish")]
    [InlineData("eval", "backtest", "--until", "9999-12-31")]   // the last day has no end tick: refused, not thrown
    [InlineData("eval", "backtest", "--arms")]
    [InlineData("eval", "backtest", "stray")]
    public async Task Eval_backtest_with_a_bad_option_is_invalid_input_and_posts_nothing(params string[] args)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));

        var exit = await Run(api, args);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
        Assert.Contains("context eval backtest", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_start_prints_SND_JOB_409_and_exits_2()
    {
        var api = new ScriptedApi((HttpStatusCode.Conflict,
            """{"type":"x","title":"SND-JOB-409","status":409,"detail":"A SyncSources job is running (JOB-0000000000000000000000000000000e)","runningJob":"JOB-0000000000000000000000000000000e"}"""));

        var exit = await Run(api, "sources", "sync", "--detach");

        Assert.Equal(2, exit);
        Assert.Contains("SND-JOB-409", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("sources", "sync", "a", "b")]
    [InlineData("sources", "sync", "--nope")]
    [InlineData("map", "build", "extra")]
    public async Task A_form_that_is_not_the_verbs_is_a_usage_error_and_calls_nothing(params string[] args)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));

        var exit = await Run(api, args);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
        Assert.Contains("[--detach] [--json]", _error.ToString() + _out, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(true, "context", "sources", "sync")]
    [InlineData(true, "context", "sources", "sync", "slack", "--json")]
    [InlineData(true, "context", "map", "build")]
    [InlineData(false, "context", "map", "build", "--detach")]
    [InlineData(false, "context", "sources", "sync", "--detach")]
    [InlineData(false, "context", "sources", "status")]
    public void The_two_verbs_listen_for_ctrl_C_unless_detached(bool follows, params string[] args) => Assert.Equal(follows, Interrupt.FollowsJob(args));
}
