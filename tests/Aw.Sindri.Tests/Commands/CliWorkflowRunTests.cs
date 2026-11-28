using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri wf run &lt;slug&gt; [--revision &lt;n&gt;]' (#3691): the revision goes in the body of POST /sindri/workflows/{slug}/runs; left out, the body has none and the live revision runs.</summary>
public sealed class CliWorkflowRunTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("drift-check");

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Body<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    private Task<int> Run(string[] args, Api api)
    {
        var client = new ControlPlaneClient(_up, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api);
        return WfRunCommand.Run(args, new RecipeDesk(client, TextReader.Null, _ => Task.FromResult((Error?)null)), new Output(_out, _error, SliceHarness.Json), Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_run_revision_sends_the_revision_and_prints_the_run_of_that_revision()
    {
        var api = new Api(HttpStatusCode.OK, Body(new RunWorkflowResponse(WorkflowRunId.Of(_slug, 2, 1), "started", Reason: null)));

        var code = await Run(["drift-check", "--revision", "2"], api);

        Assert.Equal((0, "/sindri/workflows/drift-check/runs"), (code, api.Path));
        Assert.Contains("\"revision\":2", api.Sent, StringComparison.Ordinal);
        Assert.Equal("started drift-check@2:1\n", _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_run_without_revision_sends_none()
    {
        var api = new Api(HttpStatusCode.OK, Body(new RunWorkflowResponse(WorkflowRunId.Of(_slug, 1, 1), "started", Reason: null)));

        _ = await Run(["drift-check"], api);

        Assert.Null(JsonNode.Parse(api.Sent!)!["revision"]);   // why: a run always sends a body; left out or null, the live revision runs
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("latest")]
    [InlineData("0")]
    [InlineData("two")]
    public async Task Wf_run_revision_must_be_a_number_of_1_or_more(string revision)
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Run(["drift-check", "--revision", revision], api);

        Assert.Equal((2, 0), (code, api.Calls));
        Assert.Contains("--revision", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_run_revision_prints_the_servers_refusal_of_an_unknown_revision()
    {
        var api = new Api(HttpStatusCode.NotFound, "{\"type\":\"about:blank\",\"title\":\"SND-GRAPH-008\",\"status\":404,\"detail\":\"workflow 'drift-check' has no revision 9 (its newest is 2)\"}");

        var code = await Run(["drift-check", "--revision", "9"], api);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-GRAPH-008 ", _error.ToString(), StringComparison.Ordinal);
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Path { get; private set; }

        public string? Sent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Path = request.RequestUri?.AbsolutePath;
            Sent = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, (int)status >= 400 ? "application/problem+json" : "application/json") };
        }
    }
}
