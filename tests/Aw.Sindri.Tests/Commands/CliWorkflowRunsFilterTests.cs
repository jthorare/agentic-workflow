using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>#4334: 'sindri wf runs &lt;slug&gt; --status S --since T --until T --before R' sends the route's query values as given, and says how to fetch the next page.</summary>
public sealed class CliWorkflowRunsFilterTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");
    private static readonly DateTimeOffset _at = new(2026, 11, 8, 9, 30, 0, TimeSpan.Zero);

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Body<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    private WfReadVerbs Verbs(Api api) =>
        new(new ControlPlaneClient(_up, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api),
            new Output(_out, _error, SliceHarness.Json));

    private string Text => _out.ToString().ReplaceLineEndings("\n");

    [Fact, Trait("Lane", "unit")]
    public async Task The_filter_flags_go_to_the_route_as_its_query_values()
    {
        var api = new Api(HttpStatusCode.OK, Body(new ListWorkflowRunsResponse(_slug, [])));

        var code = await Verbs(api).Runs(["morning-check", "--status", "failed,cancelled", "--since", "2026-11-23T00:00:00+01:00", "--until", "2026-11-24", "--before", "morning-check@1:40"], Token);

        Assert.Equal(0, code);
        Assert.Equal("/sindri/workflows/morning-check/runs?status=failed%2Ccancelled&since=2026-11-23T00%3A00%3A00%2B01%3A00&until=2026-11-24&before=morning-check%401%3A40", api.PathAndQuery);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_flags_the_route_gets_no_query()
    {
        var api = new Api(HttpStatusCode.OK, Body(new ListWorkflowRunsResponse(_slug, [])));

        _ = await Verbs(api).Runs(["morning-check", "--json"], Token);

        Assert.Equal("/sindri/workflows/morning-check/runs", api.PathAndQuery);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_page_with_more_ends_with_the_command_for_the_next_one()
    {
        var run = new WorkflowRunSummary(WorkflowRunId.Of(_slug, 1, 3), 1, "failed", "schedule", _at, _at.AddMinutes(1), "node 'agent' failed");
        var api = new Api(HttpStatusCode.OK, Body(new ListWorkflowRunsResponse(_slug, [run], More: true, Next: run.Id)));

        _ = await Verbs(api).Runs(["morning-check", "--status", "failed"], Token);

        Assert.Equal("morning-check@1:3  failed  schedule  2026-11-08 09:30:00Z  node 'agent' failed\nOlder runs: sindri wf runs morning-check --before morning-check@1:3 (with the same filter flags)\n", Text);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--status")]
    [InlineData("--since")]
    [InlineData("--until")]
    [InlineData("--before")]
    public async Task A_filter_flag_without_a_value_is_usage_and_calls_nothing(string flag)
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(api).Runs(["morning-check", flag], Token);

        Assert.Equal((2, 0), (code, api.Calls));
        Assert.Contains($"'{flag}' needs a value", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains(WfReadVerbs.RunsUsage, _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_flag_is_still_refused()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(api).Runs(["morning-check", "--state", "failed"], Token);

        Assert.Equal((2, 0), (code, api.Calls));
        Assert.Contains("'--state' isn't an option of wf runs", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_usage_names_the_filter_flags()
    {
        Assert.Equal("usage: sindri wf runs <slug> [--status S] [--since T] [--until T] [--before R] [--json]", WfReadVerbs.RunsUsage);
        Assert.Contains("sindri wf runs <slug> [--status S] [--since T] [--until T] [--before R] [--json]", Usage.Text, StringComparison.Ordinal);
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? PathAndQuery { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            PathAndQuery = request.RequestUri?.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, (int)status >= 400 ? "application/problem+json" : "application/json") });
        }
    }
}
