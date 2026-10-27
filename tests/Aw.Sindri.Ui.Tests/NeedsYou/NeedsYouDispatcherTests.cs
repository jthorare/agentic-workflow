using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>What a ready Home row action does when the shell's Decide hands it on: each sends once, a refusal is said as served, and an action with no send says so (never dropped silently).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouDispatcherTests
{
    private const string Instance = "WFI-1";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private NeedsYouDispatcher Dispatcher() =>
        new(_server, () => _api, new InstanceCommander(_server, () => _api), new DeskReporter(_server, () => _api));

    private static NeedsYouEntry ResumeRow() => NeedsYouRows.Park("park:" + Instance) with
    {
        PrimaryAction = "ApplyCommand:answer-resume",
        Actions = [Resume()],
        Subject = new NeedsYouSubject("instance", Instance),
    };

    private static NeedsYouAction Resume() => new("ApplyCommand:answer-resume", "Resume", ActionReadiness.Ready, "sindri workflow answer ITM-01JC8/step/1 --resume", RejectLike: false);

    private static NeedsYouAction Service(string id, string label) => new(id, label, ActionReadiness.Ready, "sindri services " + id, RejectLike: false);

    // The f5 desk golden, with the recommended outcome set or dropped; severalOutcomes serves its complete command naming two outcomes instead of "done".
    // The golden's one required output is dropped unless withOutputs, so these tests are about the outcome; the output has its own test.
    private void ServeRecommending(string? outcome, bool severalOutcomes = false, bool withOutputs = false)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "f5-desk-recommended"))!;
        var desk = instance["current"]!["desk"]!.AsObject();
        if (!withOutputs)
        {
            desk["outputs"] = new JsonArray();
        }

        if (outcome is null)
        {
            _ = desk.Remove("recommended");
        }
        else
        {
            desk["recommended"]!["outcome"] = outcome;
        }

        var body = instance.ToJsonString();
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, severalOutcomes ? body.Replace("--outcome done", "--outcome <approve|revise>", StringComparison.Ordinal) : body);
    }

    [Fact]
    public async Task Resume_sends_an_answer_resume_to_the_rows_instance_once()
    {
        var row = ResumeRow();
        var dispatcher = Dispatcher();
        _server.WorkflowCommandGate = new TaskCompletionSource();

        var first = dispatcher.Run(row, Resume());
        var second = await dispatcher.Run(row, Resume());   // a second press while the first is out
        _server.WorkflowCommandGate.SetResult();
        var sent = await first;

        var (instance, body) = Assert.Single(_server.CommandCalls);
        Assert.Equal((Instance, CommandKind.Answer, (AnswerVariant?)AnswerVariant.Resume), (instance, body.Kind, body.Answer));
        Assert.Equal((DispatchOutcome.Sent, DispatchOutcome.Busy), (sent.Outcome, second.Outcome));
    }

    [Fact]
    public async Task Refused_resume_shows_the_error_code()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-041", "That run has moved on. Read the instance again.");

        var result = await Dispatcher().Run(ResumeRow(), Resume());

        Assert.Equal(DispatchOutcome.Refused, result.Outcome);
        Assert.Contains("SND-WF-041", result.Message, StringComparison.Ordinal);
        Assert.Contains("That run has moved on. Read the instance again.", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_sends_the_recommended_outcome()
    {
        ServeRecommending("approve");

        var result = await Dispatcher().Run(NeedsYouRows.Desk(), NeedsYouRows.Desk().Actions[0]);

        var (instance, body) = Assert.Single(_server.CompleteCalls);
        Assert.Equal((NeedsYouRows.DeskInstance, "approve"), (instance, body.Outcome));
        Assert.Equal(DispatchOutcome.Sent, result.Outcome);
    }

    [Fact]
    public async Task Complete_sends_the_steps_only_outcome_when_none_is_recommended()
    {
        ServeRecommending(outcome: null);

        var result = await Dispatcher().Run(NeedsYouRows.Desk(), NeedsYouRows.Desk().Actions[0]);

        var (_, body) = Assert.Single(_server.CompleteCalls);
        Assert.Equal(("done", DispatchOutcome.Sent), (body.Outcome, result.Outcome));
    }

    [Fact]
    public async Task Complete_on_a_task_with_a_required_output_says_to_open_it_with_Answer_and_sends_nothing()
    {
        ServeRecommending("approve", withOutputs: true);

        var result = await Dispatcher().Run(NeedsYouRows.Desk(), NeedsYouRows.Desk().Actions[0]);

        Assert.Empty(_server.CompleteCalls);
        Assert.Equal((DispatchOutcome.Refused, Labels.CompleteNeedsOutput), (result.Outcome, result.Message));
    }

    [Theory]
    [InlineData("reviseDesign", false)]
    [InlineData("rework", false)]
    [InlineData("drop", false)]
    [InlineData(null, true)]
    public async Task Complete_that_needs_a_reason_or_a_choice_opens_the_sheet_and_sends_nothing(string? recommended, bool severalOutcomes)
    {
        ServeRecommending(recommended, severalOutcomes);

        var result = await Dispatcher().Run(NeedsYouRows.Desk(), NeedsYouRows.Desk().Actions[0]);

        Assert.Equal(DispatchOutcome.OpenSheet, result.Outcome);
        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task Pause_service_pauses_the_rows_service_by_name()
    {
        var result = await Dispatcher().Run(NeedsYouRows.Failure(), Service("PauseService", "Pause service"));

        Assert.Equal(["index"], _server.PauseServiceCalls);
        Assert.Equal(DispatchOutcome.Sent, result.Outcome);
    }

    [Fact]
    public async Task Resume_service_and_run_service_send_their_own_route()
    {
        var dispatcher = Dispatcher();

        _ = await dispatcher.Run(NeedsYouRows.Failure(), Service("ResumeService", "Resume service"));
        _ = await dispatcher.Run(NeedsYouRows.Failure(), Service("RunService", "Run again"));

        Assert.Equal(["index"], _server.ResumeServiceCalls);
        Assert.Equal(["index"], _server.RunServiceCalls);
    }

    [Fact]
    public async Task An_action_the_app_cannot_send_says_so_and_sends_nothing()
    {
        var other = new NeedsYouAction("SomethingNew", "Something new", ActionReadiness.Ready, "sindri something new", RejectLike: false);

        var result = await Dispatcher().Run(NeedsYouRows.Failure(), other);

        Assert.Equal((DispatchOutcome.Unhandled, Labels.RowNotAvailable("sindri something new")), (result.Outcome, result.Message));
        Assert.Empty(_server.CommandCalls);
        Assert.Empty(_server.PauseServiceCalls);
    }

    [Fact]
    public void The_resume_body_names_the_resume_answer_on_the_wire()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(ApplyCommandBody.Resume(Instance), Ui.Api.SindriUiJsonContext.Default.ApplyCommandBody);

        Assert.Contains("\"resume\"", json, StringComparison.Ordinal);
    }
}
