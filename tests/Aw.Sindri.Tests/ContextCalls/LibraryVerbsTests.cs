using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'sindri library sync' over a scripted API (PR review page spec 2026-11-04 §5.2): the library feed's own tick through the running control plane, the way 'index sync library' runs it.</summary>
public sealed class LibraryVerbsTests : IDisposable
{
    private const string Started = """{"accepted":{"commandId":"00000000-0000-0000-0000-00000000000c","sequence":5},"jobId":"JOB-0000000000000000000000000000000e","links":{"job":"/sindri/jobs/JOB-0000000000000000000000000000000e","cancel":"/sindri/jobs/JOB-0000000000000000000000000000000e/cancel","events":"/sindri/events?after=4&types=job.*"}}""";
    private const string Rows = """{"sources":[{"source":"library","phase":"incremental","usable":true,"complete":true,"lastSync":"2026-11-06T12:00:00.0000000Z","lastCode":null}]}""";

    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<int> Run(ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        var output = new Output(_out, _error, SliceHarness.Json);
        using var input = new StringReader(string.Empty);
        return await new LibraryVerbs(new IndexVerbs(client, output, new JobFollower(client, output, TimeProvider.System), terminal: false, input)).Run(args, Token, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sync_starts_the_library_sources_own_job_after_checking_it_is_one_of_the_feeds()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Rows), (HttpStatusCode.OK, Started));

        var exit = await Run(api, "sync", "--detach");

        Assert.True(exit == 0, _error.ToString());
        Assert.Equal([("GET", "/sindri/index/status"), ("POST", "/sindri/index/sync")], api.Calls.Select(c => (c.Method, c.Route)));
        Assert.Contains("\"library\"", api.Calls[1].Body, StringComparison.Ordinal);
        Assert.Equal("job JOB-0000000000000000000000000000000e started\n", _out.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sync_when_the_profile_has_no_library_feed_names_the_feeds_and_starts_nothing()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, """{"sources":[{"source":"linear","phase":null,"usable":false,"complete":false,"lastSync":null,"lastCode":null}]}"""));

        var exit = await Run(api, "sync");

        Assert.Equal(2, exit);
        _ = Assert.Single(api.Calls);
        Assert.Contains("library", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData()]
    [InlineData("sync", "harbor")]
    [InlineData("sync", "--force")]
    [InlineData("list")]
    public async Task Anything_but_sync_and_its_flags_is_a_usage_error_that_calls_nothing(params string[] args)
    {
        var api = new ScriptedApi();

        var exit = await Run(api, args);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
        Assert.Contains("library sync", _error.ToString(), StringComparison.Ordinal);
    }
}
