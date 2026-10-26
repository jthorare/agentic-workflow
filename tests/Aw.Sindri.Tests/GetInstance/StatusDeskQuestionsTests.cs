using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>'workflow status' at an interview desk lists what the desk asks (issue 998): each question with its option ids, the recommended one, other and skip, and the shape of the answers.</summary>
[Lane(TestLane.Unit)]
public sealed class StatusDeskQuestionsTests
{
    private const string Sha = "0000000000000000000000000000000000000000000000000000000000000002";

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

    private static InstanceView Desk(string step)
    {
        var view = StatusCases.View("desk-task");
        return view with
        {
            Step = new StepDeclaration("human", step, "asks the questions"),
            FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:questions"] = Sha },
        };
    }

    private static ReadApi Api(InstanceView view, string questions)
    {
        var instances = new OneInstance(view);
        return ReadApi.OverAt(instances, new FakeTimeProvider(StatusCases.Now), services => services.AddSingleton<IService<GetInstanceFactRequest, FactView>>(
            new GetInstanceFactService(instances, instances, new Contents(questions), new Plain(), new ListInstances.FixedCaller(new CallerContextBuilder().Human().Build()))));
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> Run(ReadApi api, CancellationToken cancellationToken, params string[] args)
    {
        var (stdout, stderr) = (new StringWriter(), new StringWriter());
        var exit = await Aw.Sindri.Host.Commands.StatusCommand.Run(args, api.Desk, new Output(stdout, stderr, SliceHarness.Json), cancellationToken);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    [Fact(Timeout = 30_000)]
    public async Task The_interview_desk_lists_each_question_with_its_option_ids_and_the_answer_forms()
    {
        using var api = Api(Desk("step.interview.answer"), Questions);

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.Equal(0, exit);
        Assert.Contains("Q1: What happens on expiry?", stdout, StringComparison.Ordinal);
        Assert.Contains("Q1.a  A 410 page  (recommended)", stdout, StringComparison.Ordinal);
        Assert.Contains("Q1.b  A 404\n", stdout, StringComparison.Ordinal);
        Assert.Contains("other: write your own answer; skip: leave it unanswered", stdout, StringComparison.Ordinal);
        Assert.Contains("{\"question\":\"Q1\",\"option\":\"Q1.a\"}", stdout, StringComparison.Ordinal);
        Assert.Contains("{\"question\":\"Q1\",\"skip\":true}", stdout, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Json_adds_a_questions_member_to_the_status_fields()
    {
        using var api = Api(Desk("step.interview.answer"), Questions);

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1", "--json");

        var status = JsonNode.Parse(stdout)!.AsObject();
        var question = status["questions"]![0]!;
        Assert.Equal((0, "Q1", "What happens on expiry?", "Q1.a", true, "Q1.b", false, true, true),
            (exit, (string?)question["id"], (string?)question["question"], (string?)question["options"]![0]!["id"], (bool?)question["options"]![0]!["recommended"],
             (string?)question["options"]![1]!["id"], (bool?)question["options"]![1]!["recommended"], (bool?)question["other"], (bool?)question["skip"]));
        Assert.NotNull(status["header"]);   // the status fields are all still there
    }

    [Fact(Timeout = 30_000)]
    public async Task A_desk_of_another_step_shows_no_questions_and_reads_no_fact()
    {
        using var api = Api(Desk("step.scope.stop"), Questions);

        var (_, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.DoesNotContain("Q1", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, static c => c.Contains("/facts/", StringComparison.Ordinal));
    }

    [Fact(Timeout = 30_000)]
    public async Task An_interview_desk_whose_questions_are_not_produced_prints_the_plain_status()
    {
        var view = Desk("step.interview.answer") with { FactShas = null };
        using var api = Api(view, Questions);

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.Equal((0, StatusLayout.Render(view, StatusCases.Now)), (exit, stdout));
    }

    [Fact]
    public void A_session_sees_the_question_and_option_text_inside_the_fence_and_the_ids_plain()
    {
        var questions = DeskQuestions.Questions(JsonDocument.Parse(Questions).RootElement, fenced: true);

        var question = questions[0]!;
        Assert.Equal(("Q1", "Q1.a"), ((string?)question["id"], (string?)question["options"]![0]!["id"]));
        Assert.Contains("What happens on expiry?", (string?)question["question"], StringComparison.Ordinal);
        Assert.NotEqual("What happens on expiry?", (string?)question["question"], StringComparer.Ordinal);
        Assert.NotEqual("A 410 page", (string?)question["options"]![0]!["answer"], StringComparer.Ordinal);
    }
}
