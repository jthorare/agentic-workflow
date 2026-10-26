using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// The bug check for issue 998: an interview desk's questions are not visible to the one who answers. 'workflow status' at a step.interview.answer desk
/// must list each question with its option ids and say how the answers are given, and the fact route must serve a produced fact the recipe does not declare
/// ('fact:questions' is a desk input, not a declared fact). Drives the public surface only: the status command over the API routes, and the fact service.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class DeskQuestionsCheckTests
{
    private const string Sha = "0000000000000000000000000000000000000000000000000000000000000003";

#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Questions = """
        [{"id":"Q1","dimension":"failure","question":"What happens on expiry?","options":[
          {"id":"Q1.a","answer":"A 410 page","cites":["R1"],"recommended":true},
          {"id":"Q1.b","answer":"A 404","cites":["R2"]}],"other":true,"skip":true}]
        """;
#pragma warning restore MA0136

    private sealed class Contents(string text) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(text)));
    }

    private sealed class Plain : ITextScrubber
    {
        public string Scrub(string text) => text;
    }

    /// <summary>The detail and fact routes over one instance, as the control plane answers them.</summary>
    private sealed class Routes : HttpMessageHandler
    {
        private const string Instances = "/sindri/workflow/instances";
        private readonly GetInstanceService _detail;
        private readonly GetInstanceFactService _fact;
        private readonly ListInstancesService _list;

        public Routes(OneInstance instances, string questions)
        {
            var caller = new ListInstances.FixedCaller(new CallerContextBuilder().Human().Build());
            _detail = new GetInstanceService(instances, instances, new DetailViews(), caller, new DetailContexts(new FakeTimeProvider(StatusCases.Now)), new StepRunReader(new StepRunEvents(), new StepRunRecipes()));
            _fact = new GetInstanceFactService(instances, instances, new Contents(questions), new Plain(), caller);
            var clock = new FakeTimeProvider(StatusCases.Now);
            _list = new ListInstancesService(instances, new NoRows(), new EndedInstanceViews(instances), new ListInstances.FixedItemViews(), new ListInstances.FixedCaller(current: null), new InstanceRows(clock));
            var probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
            Desk = new RecipeDesk(new ControlPlaneClient(probe, SliceHarness.Json, handler: this), TextReader.Null, static _ => throw new InvalidOperationException("a read never starts the control plane"));
        }

        public RecipeDesk Desk { get; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath == Instances)
            {
                var query = HttpUtility.ParseQueryString(uri.Query);
                var item = query["item"];
                var list = new ListInstancesRequest(query["needsMe"], item is null ? null : ItemKey.From(item), query["state"], query["query"], query["address"], query["recipe"], query["urgency"], query["subject"], query["root"], query["open"]);
                return Answer(await _list.Handle(list, cancellationToken).ConfigureAwait(false));
            }

            var rest = uri.AbsolutePath[(Instances.Length + 1)..].Split('/');
            return rest switch
            {
                [var id] => Answer(await _detail.Handle(new GetInstanceRequest(id), cancellationToken).ConfigureAwait(false)),
                [var id, "facts", var key] => Answer(await _fact.Handle(new GetInstanceFactRequest(WorkflowInstanceId.From(InstanceRoute.Id(id)), Uri.UnescapeDataString(key)), cancellationToken).ConfigureAwait(false)),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private sealed class NoRows : IWorkflowInstances
        {
            public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
                List(InstanceFilters.ToQuery(filter), ended, cancellationToken);

            public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) =>
                ValueTask.FromResult<Result<IReadOnlyList<InstanceRow>>>(Array.Empty<InstanceRow>());
        }

        private static HttpResponseMessage Answer<T>(Result<T> result) => result.IsSuccess
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(AwJson.Serialize(result.Value, SliceHarness.Json)) { Headers = { ContentType = new("application/json") } } }
            : new HttpResponseMessage(result.Error.Kind == ErrorKind.NotFound ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { type = "about:blank", title = result.Error.Code, detail = result.Error.Message }), Encoding.UTF8, "application/problem+json"),
            };
    }

    private static InstanceView Desk(string step)
    {
        var view = StatusCases.View("desk-task");
        return view with
        {
            Step = new StepDeclaration("human", step, "asks the questions"),
            FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:questions"] = Sha },
        };
    }

    private static async Task<(int Exit, string Stdout)> Status(CancellationToken cancellationToken, params string[] args)
    {
        using var routes = new Routes(new OneInstance(Desk("step.interview.answer")), Questions);
        var (stdout, stderr) = (new StringWriter(), new StringWriter());
        var exit = await Aw.Sindri.Host.Commands.StatusCommand.Run(args, routes.Desk, new Output(stdout, stderr, SliceHarness.Json), cancellationToken);
        return (exit, stdout.ToString());
    }

    [Fact(Timeout = 30_000)]
    public async Task Status_at_the_interview_desk_lists_each_question_with_its_option_ids()
    {
        var (exit, stdout) = await Status(TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.Equal(0, exit);
        Assert.Contains("What happens on expiry?", stdout, StringComparison.Ordinal);
        Assert.Contains("Q1.a", stdout, StringComparison.Ordinal);
        Assert.Contains("A 410 page", stdout, StringComparison.Ordinal);
        Assert.Contains("Q1.b", stdout, StringComparison.Ordinal);
        Assert.Contains("A 404", stdout, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Status_at_the_interview_desk_says_how_the_answers_are_given()
    {
        var (_, stdout) = await Status(TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.Contains("fact:answers", stdout, StringComparison.Ordinal);
        Assert.Contains("\"skip\":true", stdout, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Json_status_at_the_interview_desk_carries_the_questions()
    {
        var (exit, stdout) = await Status(TestContext.Current.CancellationToken, "T-3/step/1", "--json");

        Assert.Equal(0, exit);
        var questions = JsonNode.Parse(stdout)!.AsObject()["questions"];
        Assert.NotNull(questions);
        var options = questions[0]!["options"]!.AsArray();
        Assert.Equal(("Q1", "Q1.a", "Q1.b"), ((string?)questions[0]!["id"], (string?)options[0]!["id"], (string?)options[1]!["id"]));
    }

    [Fact]
    public async Task The_fact_route_serves_a_produced_fact_the_recipe_does_not_declare()
    {
        var instances = new SomeInstances(Desk("step.interview.answer") with { Facts = [] });
        var service = new GetInstanceFactService(instances, instances, new Contents(Questions), new Plain(), new ListInstances.FixedCaller(new CallerContextBuilder().Human().Build()));

        var result = await service.Handle(new GetInstanceFactRequest(Desk("step.interview.answer").Id, "fact:questions"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(("fact:questions", JsonValueKind.Array, "Q1"), (result.Value.Key, result.Value.Body.ValueKind, result.Value.Body[0].GetProperty("id").GetString()));
    }
}
