using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.AgentNodeControl;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri wf takeover|handback &lt;run&gt; &lt;node&gt; [--item N] [--json]' (#2472): posts to the node's route through the control plane and prints where the node stands; a refusal prints its code.</summary>
public sealed class CliAgentNodeTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(WorkflowSlug.From("morning-check"), 1, 7);
    private static readonly string _route = $"/sindri/workflow-runs/{Uri.EscapeDataString(_run.Value)}/nodes/agent";

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Text => _out.ToString().ReplaceLineEndings("\n");

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_takeover_posts_the_item_index_and_says_the_node_is_taken_over()
    {
        var api = new Recorder(HttpStatusCode.OK, Answer("taken_over", 0));

        var code = await Run(api, ensure: null, c => WfAgentNodeCommand.TakeOver([_run.Value, "agent"], c.Desk, c.Output, Token));

        Assert.Equal(0, code);
        Assert.Equal($"{_run.Value} agent taken over\n", Text);
        Assert.Equal((_route + "/takeover", "{\"itemIndex\":0}"), api.Sent.Single());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_handback_with_an_item_posts_it_and_says_the_node_is_back_with_its_agent()
    {
        var api = new Recorder(HttpStatusCode.OK, Answer("waiting", 2));

        var code = await Run(api, ensure: null, c => WfAgentNodeCommand.HandBack([_run.Value, "agent", "--item", "2"], c.Desk, c.Output, Token));

        Assert.Equal(0, code);
        Assert.Equal($"{_run.Value} agent handed back\n", Text);
        Assert.Equal((_route + "/handback", "{\"itemIndex\":2}"), api.Sent.Single());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_takeover_json_prints_the_body_as_received()
    {
        var body = Answer("taken_over", 0);

        var code = await Run(new Recorder(HttpStatusCode.OK, body), ensure: null, c => WfAgentNodeCommand.TakeOver([_run.Value, "agent", "--json"], c.Desk, c.Output, Token));

        Assert.Equal(0, code);
        Assert.Equal(System.Text.Json.Nodes.JsonNode.Parse(body)!.ToJsonString(), System.Text.Json.Nodes.JsonNode.Parse(Text)!.ToJsonString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refusal_prints_its_code_and_exits_2()
    {
        var problem = """{"type":"about:blank","title":"SND-GRAPH-110","status":409,"detail":"node 'agent' is not taken over, so there is nothing to hand back"}""";

        var code = await Run(new Recorder(HttpStatusCode.Conflict, problem), ensure: null, c => WfAgentNodeCommand.HandBack([_run.Value, "agent"], c.Desk, c.Output, Token));

        Assert.Equal(2, code);
        Assert.StartsWith("SND-GRAPH-110 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_control_plane_that_is_down_exits_1_and_posts_nothing()
    {
        var api = new Recorder(HttpStatusCode.OK, Answer("taken_over", 0));

        var code = await Run(api, SindriErrors.NotRunning.With("the control plane is not running"), c => WfAgentNodeCommand.TakeOver([_run.Value, "agent"], c.Desk, c.Output, Token));

        Assert.Equal(1, code);
        Assert.Empty(api.Sent);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("only-a-run")]
    [InlineData("a@1:1 agent extra")]
    [InlineData("a@1:1 agent --item")]
    [InlineData("a@1:1 agent --item x")]
    [InlineData("a@1:1 agent --item -1")]
    [InlineData("a@1:1 agent --bogus")]
    public async Task Wrong_arguments_exit_2_with_the_usage_and_post_nothing(string line)
    {
        var args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var api = new Recorder(HttpStatusCode.OK, Answer("taken_over", 0));

        var code = await Run(api, ensure: null, c => WfAgentNodeCommand.TakeOver(args, c.Desk, c.Output, Token));

        Assert.Equal(2, code);
        Assert.Contains("usage: sindri wf takeover <run> <node> [--item <index>] [--json]", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Sent);
    }

    [Fact, Trait("Lane", "unit")]
    public void Usage_and_the_catalog_name_the_same_verbs()
    {
        var rows = new Host.Catalog.CommandCatalog().Rows;

        foreach (var (action, verb) in new[] { ("TakeOverAgentNode", "takeover"), ("HandBackAgentNode", "handback") })
        {
            var row = Assert.Single(rows, r => r.Action.Value == action);
            Assert.Equal($"sindri wf {verb} <run> <node> [--item <index>]", row.Cli.Text);
            Assert.Contains($"sindri wf {verb} <run> <node> [--item <index>] [--json]", Usage.Text, StringComparison.Ordinal);
        }
    }

    private static string Answer(string agent, int item) =>
        Encoding.UTF8.GetString(AwJson.Serialize(new AgentNodeControlled(_run, NodeId.From("agent"), item, agent), SliceHarness.Json));

    private async Task<int> Run(Recorder api, Aw.Kernel.Error? ensure, Func<(RecipeDesk Desk, Output Output), Task<int>> call)
    {
        var client = new ControlPlaneClient(_up, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api);
        var desk = new RecipeDesk(client, TextReader.Null, _ => Task.FromResult(ensure));
        return await call((desk, new Output(_out, _error, SliceHarness.Json)));
    }

    private sealed class Recorder(HttpStatusCode status, string body) : HttpMessageHandler
    {
        private readonly List<(string Path, string Body)> _sent = [];

        public IReadOnlyList<(string Path, string Body)> Sent => _sent;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var text = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken) : string.Empty;
            _sent.Add((request.RequestUri!.AbsolutePath, text));
            var contentType = (int)status >= 400 ? "application/problem+json" : "application/json";
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };
        }
    }
}
