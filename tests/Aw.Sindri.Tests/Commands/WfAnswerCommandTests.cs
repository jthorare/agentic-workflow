using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AnswerQuestion;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri answer &lt;run&gt;/&lt;node&gt; (--text T | --choice C)': one POST to the node's answer route with exactly the one field, a refusal printed with its code, and bad arguments refused before any call.</summary>
[Lane(TestLane.Unit)]
public sealed class WfAnswerCommandTests : IDisposable
{
    private const string Route = "/sindri/workflow-runs/nightly%403%3A12/nodes/ask/answer";
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<int> Run(RoutedApi api, params string[] args)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        var desk = new RecipeDesk(client, TextReader.Null, _ => Task.FromResult<Kernel.Error?>(null), static _ => null);
        return await WfAnswerCommand.Run(args, desk, new Output(_out, _error, SliceHarness.Json), Token);
    }

    [Fact]
    public async Task A_text_answer_posts_only_the_text_and_prints_who_answered()
    {
        var api = new RoutedApi().OnPost(Route, HttpStatusCode.OK, new AnswerQuestionResponse(WorkflowRunId.From("nightly@3:12"), NodeId.From("ask"), "ship it", "owner"));

        var code = await Run(api, "nightly@3:12/ask", "--text", "ship it");

        Assert.Equal(0, code);
        Assert.Contains("nightly@3:12/ask answered by owner", _out.ToString(), StringComparison.Ordinal);
        var call = Assert.Single(api.Calls, static c => c.Method == "POST");
        var body = System.Text.Json.Nodes.JsonNode.Parse(call.Body!)!;
        Assert.Equal("ship it", body["text"]!.GetValue<string>());
        Assert.Null(body["choice"]);
    }

    [Fact]
    public async Task A_refusal_is_printed_with_its_code_and_exits_2()
    {
        var api = new RoutedApi().OnRaw(Route, HttpStatusCode.Conflict, """{"type":"about:blank","title":"SND-GRAPH-130","status":409,"detail":"'ask' is not waiting for an answer"}""");

        var code = await Run(api, "nightly@3:12/ask", "--choice", "yes");

        Assert.Equal(2, code);
        Assert.Contains("SND-GRAPH-130", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nightly@3:12/ask")]
    [InlineData("nightly@3:12/ask", "--text", "a", "--choice", "b")]
    [InlineData("nightly@3:12", "--text", "a")]
    [InlineData("--text", "a")]
    [InlineData("nightly@3:12/ask", "--text")]
    public async Task Bad_arguments_are_refused_before_any_call(params string[] args)
    {
        var api = new RoutedApi();

        var code = await Run(api, args);

        Assert.Equal(2, code);
        Assert.Contains("usage: sindri answer", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, static c => c.Method == "POST");
    }
}
