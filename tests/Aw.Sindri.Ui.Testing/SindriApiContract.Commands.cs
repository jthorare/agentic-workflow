using System.Net;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The workflow command routes every <see cref="Api.ISindriApi"/> passes: the desk report.</summary>
public abstract partial class SindriApiContract
{
    private const string DeskInstance = "WFI-44393da815f188cea9c6fe30ae2a30a6";

    [Fact]
    public async Task CompleteStep_sends_the_run_the_outcome_and_the_outputs_to_the_instance()
    {
        var server = new FakeSindriApi();
        var body = new CompleteStepBody(DeskInstance, "1.1", "revise", new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["fact:answers"] = "cut the scope"u8.ToArray() });

        var accepted = (await Reach(server).CompleteStep(_api, DeskInstance, body, Token)).Value;

        Expect.Equal(expected: false, accepted.AlreadyApplied);
        var (instance, sent) = server.CompleteCalls.Single();
        Expect.Equal((DeskInstance, "1.1", "revise"), (instance, sent.Run, sent.Outcome));
        Expect.Equal("cut the scope", System.Text.Encoding.UTF8.GetString(sent.Outputs["fact:answers"]));
    }

    [Fact]
    public async Task CompleteStep_refused_is_a_problem_with_the_served_words_not_an_exception()
    {
        var server = new FakeSindriApi { CompleteAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-040", "That run is not open. Read the instance again.") };

        var result = await Reach(server).CompleteStep(_api, DeskInstance, new CompleteStepBody(DeskInstance, "1.1", "approve", new Dictionary<string, byte[]>(StringComparer.Ordinal)), Token);

        Expect.Equal(("SND-WF-040", "That run is not open. Read the instance again."), (result.Error.Code, result.Error.Message));
    }

    [Fact]
    public async Task ApplyCommand_sends_the_kind_and_the_answer_to_the_instance()
    {
        var server = new FakeSindriApi();

        var accepted = (await Reach(server).ApplyCommand(_api, DeskInstance, new ApplyCommandBody(DeskInstance, CommandKind.Answer, AnswerVariant.Text, "focus on the retry"), Token)).Value;

        Expect.Equal(expected: false, accepted.AlreadyApplied);
        var (instance, sent) = server.CommandCalls.Single();
        Expect.Equal((DeskInstance, CommandKind.Answer, AnswerVariant.Text, "focus on the retry"), (instance, sent.Kind, sent.Answer, sent.Text));
    }

    [Fact]
    public async Task ApplyCommand_refused_is_a_problem_with_the_served_words_not_an_exception()
    {
        var server = new FakeSindriApi { CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-041", "That run has moved on. Read the instance again.") };

        var result = await Reach(server).ApplyCommand(_api, DeskInstance, new ApplyCommandBody(DeskInstance, CommandKind.Handback), Token);

        Expect.Equal(("SND-WF-041", "That run has moved on. Read the instance again."), (result.Error.Code, result.Error.Message));
    }
}
