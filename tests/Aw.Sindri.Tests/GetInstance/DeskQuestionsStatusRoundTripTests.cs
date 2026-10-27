using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using CompleteCommand = Aw.Sindri.Host.Commands.CompleteCommand;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// The done-when of issue 998, end to end through the CLI's own handlers: a client that reads only 'workflow status' at an interview desk learns the questions, their option ids, the
/// recommended option and the other and skip forms (text and --json); builds a 'fact:answers' file from that output; and 'workflow complete --output fact:answers=&lt;file&gt;' sends exactly that file, which the
/// answer readers take as answers. The entry forms documented next to the complete hint are the forms those readers accept, and the hint names the outcomes the desk step declares.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class DeskQuestionsStatusRoundTripTests
{
    private const string Address = "T-3/step/1";

#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Questions = """
        [{"id":"Q1","dimension":"failure","question":"What happens on expiry?","options":[
          {"id":"Q1.a","answer":"A 410 page","cites":["R1"],"recommended":true},
          {"id":"Q1.b","answer":"A 404","cites":["R2"]}],"other":true,"skip":true},
         {"id":"Q2","dimension":"scope","question":"Which links expire?","options":[
          {"id":"Q2.a","answer":"All of them","cites":["R3"]},
          {"id":"Q2.b","answer":"Only shared ones","cites":["R4"],"recommended":true}],"other":false,"skip":true}]
        """;
#pragma warning restore MA0136

    private static readonly string[] _statusLines =
    [
        "Q1: What happens on expiry?", "Q1.a  A 410 page  (recommended)", "Q1.b  A 404\n", "other: write your own answer; skip: leave it unanswered",
        "Q2: Which links expire?", "Q2.a  All of them\n", "Q2.b  Only shared ones  (recommended)",
    ];

    private static readonly string[] _hintMembers = ["\"round\"", "\"actor\"", "\"at\"", "\"questionIds\"", "\"answers\"", "--output fact:answers=<file>"];

    [GeneratedRegex(@"--run (?<run>\S+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RunOption { get; }

    [GeneratedRegex(@"\{""question"":[^{}]*\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EntryForm { get; }

    private sealed class Contents(string text) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(text)));
    }

    private sealed class Plain : ITextScrubber
    {
        public string Scrub(string text) => text;
    }

    /// <summary>The detail, list and fact routes of one interview desk, and a recording 'complete' route, as the control plane answers them.</summary>
    private sealed class Routes : HttpMessageHandler
    {
        private const string Instances = "/sindri/workflow/instances";
        private readonly InstanceView _view;
        private readonly GetInstanceService _detail;
        private readonly GetInstanceFactService _fact;
        private readonly ListInstancesService _list;

        public Routes()
        {
            var view = StatusCases.View("desk-task");
            _view = view with
            {
                Step = new StepDeclaration("human", "step.interview.answer", "asks the questions"),
                FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:questions"] = new string('0', 63) + "4" },
            };
            var instances = new OneInstance(_view);
            var clock = new FakeTimeProvider(StatusCases.Now);
            var caller = new ListInstances.FixedCaller(new CallerContextBuilder().Human().Build());
            _detail = new GetInstanceService(instances, instances, new DetailViews(), caller, new DetailContexts(clock), new StepRunReader(new StepRunEvents(), new StepRunRecipes()));
            _fact = new GetInstanceFactService(instances, instances, new Contents(Questions), new Plain(), caller);
            _list = new ListInstancesService(instances, new NoRows(), new EndedInstanceViews(instances), new ListInstances.FixedItemViews(), new ListInstances.FixedCaller(current: null), new InstanceRows(clock));
            var probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
            Desk = new RecipeDesk(new ControlPlaneClient(probe, SliceHarness.Json, handler: this), TextReader.Null, static _ => Task.FromResult<Error?>(null));   // complete checks it's running first: it is
        }

        public RecipeDesk Desk { get; }

        /// <summary>The report the complete route received, or null.</summary>
        public CompleteStepRequest? Completed { get; private set; }

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
            if (request.Method == HttpMethod.Post && rest is [_, "runs", _, "complete"])
            {
                var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                var sent = AwJson.Deserialize<CompleteStepRequest>(body, SliceHarness.Json).Value;
                Completed = sent;
                return Answer(Results.Success(new CompleteStepResponse(new CommandResult(_view, "-> checks"), sent.Nonce ?? CommandNonce.From(new string('a', 32)), AlreadyApplied: false)));
            }

            return rest switch
            {
                [var id] => Answer(await _detail.Handle(new GetInstanceRequest(id), cancellationToken).ConfigureAwait(false)),
                [var id, "facts", var key] => Answer(await _fact.Handle(new GetInstanceFactRequest(WorkflowInstanceId.From(InstanceRoute.Id(id)), Uri.UnescapeDataString(key)), cancellationToken).ConfigureAwait(false)),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private static HttpResponseMessage Answer<T>(Result<T> result) => result.IsSuccess
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(AwJson.Serialize(result.Value, SliceHarness.Json)) { Headers = { ContentType = new("application/json") } } }
            : new HttpResponseMessage(result.Error.Kind == ErrorKind.NotFound ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { type = "about:blank", title = result.Error.Code, detail = result.Error.Message }), Encoding.UTF8, "application/problem+json"),
            };

        private sealed class NoRows : IWorkflowInstances
        {
            public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
                List(InstanceFilters.ToQuery(filter), ended, cancellationToken);

            public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) =>
                ValueTask.FromResult<Result<IReadOnlyList<InstanceRow>>>(Array.Empty<InstanceRow>());
        }
    }

    private static async Task<(int Exit, string Stdout)> Status(Routes routes, CancellationToken cancellationToken, params string[] extra)
    {
        var (stdout, stderr) = (new StringWriter(), new StringWriter());
        var exit = await Host.Commands.StatusCommand.Run([Address, .. extra], routes.Desk, new Output(stdout, stderr, SliceHarness.Json), cancellationToken);
        return (exit, stdout.ToString());
    }

    private static string Recommended(JsonNode question) =>
        (string)question["options"]!.AsArray().Single(static o => (bool)o!["recommended"]!)!["id"]!;

    [Fact(Timeout = 30_000)]
    public async Task Status_text_lists_every_question_with_its_option_ids_the_recommended_option_and_the_other_and_skip_forms()
    {
        using var routes = new Routes();

        var (exit, text) = await Status(routes, TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.All(_statusLines, expected => Assert.Contains(expected, text, StringComparison.Ordinal));
        Assert.Equal(2, Regex.Count(text, "skip: leave it unanswered", RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    [Fact(Timeout = 30_000)]
    public async Task Status_json_carries_each_question_with_its_recommended_option_other_and_skip()
    {
        using var routes = new Routes();

        var (exit, json) = await Status(routes, TestContext.Current.CancellationToken, "--json");

        var questions = JsonNode.Parse(json)!.AsObject()["questions"]!.AsArray();
        Assert.Equal(0, exit);
        Assert.Equal(
            [("Q1", "Q1.a", true, true), ("Q2", "Q2.b", false, true)],
            questions.Select(static q => ((string)q!["id"]!, Recommended(q), (bool)q["other"]!, (bool)q["skip"]!)));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_client_that_reads_only_status_builds_the_answers_file_and_complete_sends_it_whole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var routes = new Routes();
        using var home = new TempHome();
        var (_, text) = await Status(routes, cancellationToken);
        var (_, json) = await Status(routes, cancellationToken, "--json");
        var run = RunOption.Match(text).Groups["run"].Value;   // the complete line status prints
        var questions = JsonNode.Parse(json)!.AsObject()["questions"]!.AsArray();
        var batch = new JsonObject
        {
            ["round"] = 1,
            ["actor"] = "owner",
            ["at"] = "2026-10-25T00:00:00Z",
            ["questionIds"] = new JsonArray([.. questions.Select(static q => (JsonNode)JsonValue.Create((string)q!["id"]!)!)]),
            ["answers"] = new JsonArray([.. questions.Select(static q => (JsonNode)new JsonObject { ["question"] = (string)q!["id"]!, ["option"] = Recommended(q) })]),
        };
        var file = Path.Combine(home.Path, "answers.json");
        await File.WriteAllTextAsync(file, new JsonArray(batch).ToJsonString(), cancellationToken);
        var (stdout, stderr) = (new StringWriter(), new StringWriter());
        var services = new ServiceCollection().AddSingleton<TimeProvider>(new FakeTimeProvider(StatusCases.Now)).BuildServiceProvider();

        var exit = await CompleteCommand.Run(
            [Address, "--run", run, "--outcome", "answered", "--output", $"fact:answers={file}", "--nonce", new string('a', 32)], routes.Desk, services, new Output(stdout, stderr, SliceHarness.Json), cancellationToken);

        Assert.Equal((0, true), (exit, stdout.ToString().Contains("-> checks", StringComparison.Ordinal)));
        var sentAnswers = Assert.IsType<CompleteStepRequest>(routes.Completed).Outputs[FactKey.From("fact:answers")];
        Assert.Equal(await File.ReadAllBytesAsync(file, cancellationToken), sentAnswers.ToArray());
        Assert.Equal(
            [("Q1", "Q1.a", null, false), ("Q2", "Q2.b", null, false)],
            AnswerEntry.Latest(JsonNode.Parse(sentAnswers.Span)).Select(static a => (a.Question, a.Option, a.Text, a.Skipped)));
    }

    [Fact]
    public void The_entry_forms_the_complete_hint_documents_are_the_forms_the_answer_readers_accept()
    {
        var forms = EntryForm.Matches(DeskQuestions.AnswerHint).Select(static m => JsonNode.Parse(m.Value)).ToList();

        var entries = AnswerEntry.Of(new JsonObject { ["answers"] = new JsonArray([.. forms]) }).ToList();

        Assert.Equal(
            [("Q1", "Q1.a", null, false), ("Q1", null, "your own answer", false), ("Q1", null, null, true)],
            entries.Select(static a => (a.Question, a.Option, a.Text, a.Skipped)));
    }

    [Fact]
    public void The_hint_names_the_batch_members_the_output_option_and_the_outcomes_the_desk_step_declares()
    {
        var hint = DeskQuestions.AnswerHint;

        Assert.All(_hintMembers, member => Assert.Contains(member, hint, StringComparison.Ordinal));
        Assert.All(ScopingSteps.InterviewAnswer.Outcomes, outcome => Assert.Contains($"--outcome {outcome.Value}", hint, StringComparison.Ordinal));
    }
}
