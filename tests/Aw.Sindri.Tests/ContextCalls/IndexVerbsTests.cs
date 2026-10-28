using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'sindri index sync|status|purge' over a scripted API (owned-index spec §5.8): what each sends and prints, and the exit codes (0 ok, 1 attention or not running, 2 error).</summary>
public sealed class IndexVerbsTests : IDisposable
{
    private const string Started = """{"accepted":{"commandId":"00000000-0000-0000-0000-00000000000c","sequence":5},"jobId":"JOB-0000000000000000000000000000000e","links":{"job":"/sindri/jobs/JOB-0000000000000000000000000000000e","cancel":"/sindri/jobs/JOB-0000000000000000000000000000000e/cancel","events":"/sindri/events?after=4&types=job.*"}}""";
    private const string Rows = """{"sources":[{"source":"linear","phase":"incremental","usable":true,"complete":true,"lastSync":"2026-10-27T12:00:00.0000000Z","lastCode":null},{"source":"github","phase":"recent","usable":false,"complete":false,"lastSync":null,"lastCode":"SND-CONTEXT-010"},{"source":"slack","phase":null,"usable":false,"complete":false,"lastSync":null,"lastCode":null}]}""";
    private const string Preview = """{"source":"github","records":8200,"chunks":31000,"vectors":31000,"purged":false}""";
    private const string Purged = """{"source":"github","records":8200,"chunks":31000,"vectors":31000,"purged":true}""";

    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Printed => _out.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> Run(ScriptedApi api, string typed, bool terminal, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        var output = new Output(_out, _error, SliceHarness.Json);
        using var input = new StringReader(typed);
        return await new IndexVerbs(client, output, new JobFollower(client, output, TimeProvider.System), terminal, input).Run(args, Token, Token);
    }

    private Task<int> Run(ScriptedApi api, params string[] args) => Run(api, typed: string.Empty, terminal: false, args);

    private static (HttpStatusCode, string) Problem(HttpStatusCode status, string code, string detail) =>
        (status, string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"type":"x","title":"{{code}}","status":{{(int)status}},"detail":"{{detail}}"}"""));

    [Fact, Trait("Lane", "unit")]
    public async Task Status_prints_a_line_per_source_and_exits_1_while_a_source_ended_with_a_code()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Rows));

        var exit = await Run(api, "status");

        Assert.Equal(("GET", "/sindri/index/status"), (api.Calls[0].Method, api.Calls[0].Route));
        Assert.Equal(1, exit);
        Assert.Equal(
            "linear  complete · last sync 2026-10-27 12:00:00Z\n" +
            "github  last sync failed: SND-CONTEXT-010 · not synced yet\n" +
            "slack   not started · not synced yet\n",
            Printed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_never_calls_a_source_usable_when_its_last_sync_failed()
    {
        const string rows = """{"sources":[{"source":"linear","phase":"backfill","usable":true,"complete":false,"lastSync":"2026-10-27T12:00:00.0000000Z","lastCode":"SND-CONTEXT-072"},{"source":"github","phase":"backfill","usable":true,"complete":false,"lastSync":"2026-10-27T12:00:00.0000000Z","lastCode":"SND-CONTEXT-059"},{"source":"slack","phase":null,"usable":false,"complete":false,"lastSync":null,"lastCode":null}]}""";

        _ = await Run(new ScriptedApi((HttpStatusCode.OK, rows)), "status");

        var lines = Printed.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var linear = Assert.Single(lines, l => l.StartsWith("linear", StringComparison.Ordinal));
        Assert.Contains("last sync failed: SND-CONTEXT-072", linear, StringComparison.Ordinal);
        Assert.DoesNotContain("usable", linear, StringComparison.Ordinal);
        Assert.Contains("usable", Assert.Single(lines, l => l.StartsWith("github", StringComparison.Ordinal)), StringComparison.Ordinal);   // the time cap is not a failure
        Assert.Contains("not synced yet", Assert.Single(lines, l => l.StartsWith("slack", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_exits_0_when_no_source_ended_with_a_code_and_json_prints_the_body_as_it_came()
    {
        const string Quiet = """{"sources":[{"source":"linear","phase":"incremental","usable":true,"complete":true,"lastSync":"2026-10-27T12:00:00.0000000Z","lastCode":null}]}""";

        var exit = await Run(new ScriptedApi((HttpStatusCode.OK, Quiet)), "status", "--json");

        Assert.Equal(0, exit);
        Assert.Contains("\"source\": \"linear\"", Printed.Replace("\"source\":\"linear\"", "\"source\": \"linear\"", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_with_no_record_source_says_so_and_exits_0()
    {
        var exit = await Run(new ScriptedApi((HttpStatusCode.OK, """{"sources":[]}""")), "status");

        Assert.Equal(0, exit);
        Assert.Contains("no record source", Printed, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_while_the_control_plane_is_down_exits_1()
    {
        var exit = await Run(new ScriptedApi(Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "Sindri isn't running")), "status");

        Assert.Equal(1, exit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sync_runs_the_index_sync_service_and_with_detach_prints_the_job()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));

        var exit = await Run(api, "sync", "--detach");

        Assert.True(exit == 0, _error.ToString());
        var (method, route, _) = Assert.Single(api.Calls);
        Assert.Equal(("POST", "/sindri/services/index-sync/run"), (method, route));
        Assert.Equal("job JOB-0000000000000000000000000000000e started\n", Printed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sync_of_a_named_source_checks_the_source_is_one_of_the_feeds_first()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Rows), (HttpStatusCode.OK, Started));

        var exit = await Run(api, "sync", "github", "--detach");

        Assert.Equal(0, exit);
        Assert.Equal(["/sindri/index/status", "/sindri/services/index-sync/run"], api.Calls.Select(c => c.Route), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sync_of_an_unknown_source_names_the_feeds_and_starts_nothing()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Rows));

        var exit = await Run(api, "sync", "jira");

        Assert.Equal(2, exit);
        _ = Assert.Single(api.Calls);
        Assert.Contains("linear, github, slack", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_shows_the_counts_then_deletes_after_a_typed_yes()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Preview), (HttpStatusCode.OK, Purged));

        var exit = await Run(api, typed: "yes\n", terminal: true, "purge", "github");

        Assert.True(exit == 0, _error.ToString());
        Assert.Equal(["""{"source":"github","confirm":false}""", """{"source":"github","confirm":true}"""], api.Calls.Select(c => c.Body), StringComparer.Ordinal);
        Assert.All(api.Calls, c => Assert.Equal(("POST", "/sindri/index/purge"), (c.Method, c.Route)));
        Assert.Contains("deletes 8,200 records, 31,000 chunks, 31,000 vectors", _error.ToString() + Printed, StringComparison.Ordinal);
        Assert.Contains("purged github: 8,200 records", Printed, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("no\n")]
    [InlineData("y\n")]
    [InlineData("")]
    public async Task Purge_answered_with_anything_but_yes_deletes_nothing_and_exits_1(string typed)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Preview));

        var exit = await Run(api, typed, terminal: true, "purge", "github");

        Assert.Equal(1, exit);
        _ = Assert.Single(api.Calls);
        Assert.Contains("nothing was deleted", _error.ToString() + Printed, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_without_a_terminal_asks_nothing_deletes_nothing_and_exits_2()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Preview));

        var exit = await Run(api, typed: "yes\n", terminal: false, "purge", "github");

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
        Assert.Contains("terminal", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_by_a_session_is_refused_at_the_preview_and_exits_1()
    {
        var api = new ScriptedApi(Problem(HttpStatusCode.Forbidden, "SND-AUTH-002", "index purge is the owner's"));

        var exit = await Run(api, typed: "yes\n", terminal: true, "purge", "github");

        Assert.Equal(1, exit);
        _ = Assert.Single(api.Calls);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("purge")]
    [InlineData("purge", "a", "b")]
    [InlineData("status", "x")]
    [InlineData("sync", "a", "b")]
    [InlineData("sync", "--force")]
    [InlineData("frobnicate")]
    public async Task A_bad_shape_is_a_usage_error_that_calls_nothing(params string[] args)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Rows));

        var exit = await Run(api, args);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
        Assert.Contains("index sync", _error.ToString(), StringComparison.Ordinal);
    }
}
