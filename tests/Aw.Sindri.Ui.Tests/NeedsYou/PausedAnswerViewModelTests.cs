using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The answer form of a paused run (needs-you spec §3, §5, slice 4): what each option sends, and that it is sent once per press.</summary>
[Lane(TestLane.Unit)]
public sealed class PausedAnswerViewModelTests
{
    private const string Instance = "WFI-1";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new();

    private PausedAnswerViewModel Form(NeedsYouEntry? row = null)
    {
        var form = new PausedAnswerViewModel(new InstanceCommander(_server, () => _api), _clipboard, _clock);
        form.Show(row ?? Paused());
        return form;
    }

    private static NeedsYouEntry Paused() => NeedsYouRows.Grant("park:" + Instance) with { Subject = new NeedsYouSubject("instance", Instance) };

    [Fact]
    public async Task The_chosen_hint_is_sent_once_per_press_as_an_answer_text()
    {
        var form = Form();
        form.Choose(PausedAnswerOption.Hint);
        form.HintText = "  focus on the retry loop ";
        _server.WorkflowCommandGate = new TaskCompletionSource();

        var first = form.Send();
        await form.Send();   // a second press while the first is out
        _server.WorkflowCommandGate.SetResult();
        await first;

        var (instance, body) = Assert.Single(_server.CommandCalls);
        Assert.Equal((Instance, CommandKind.Answer, (AnswerVariant?)AnswerVariant.Text, (string?)"focus on the retry loop"), (instance, body.Kind, body.Answer, body.Text));
        Assert.True(form.IsSent);
    }

    [Fact]
    public async Task Stop_sends_an_answer_reject_and_nothing_else()
    {
        var form = Form();
        form.Choose(PausedAnswerOption.Stop);

        await form.Send();

        var (_, body) = Assert.Single(_server.CommandCalls);
        Assert.Equal((CommandKind.Answer, (AnswerVariant?)AnswerVariant.Reject, null), (body.Kind, body.Answer, body.Text));
        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task A_hint_with_no_text_sends_nothing()
    {
        var form = Form();
        form.Choose(PausedAnswerOption.Hint);
        form.HintText = "   ";

        await form.Send();

        Assert.Equal((false, 0), (form.CanSend, _server.CommandCalls.Count));
    }

    [Fact]
    public async Task More_tries_copies_the_served_command_with_the_stepper_rounds_and_sends_nothing()
    {
        var form = Form();
        form.Increase();
        form.Increase();

        await form.Send();

        Assert.Equal(("Allow more tries · terminal", "sindri workflow answer ITM-01JC8/step/1 --grant 3"), (form.SendWord, _clipboard.Text));
        Assert.Empty(_server.CommandCalls);
    }

    [Fact]
    public void The_stepper_stays_between_one_and_ten()
    {
        var form = Form();

        form.Decrease();
        Assert.Equal(1, form.ExtraTries);
        for (var i = 0; i < 20; i++)
        {
            form.Increase();
        }

        Assert.Equal(10, form.ExtraTries);
    }

    [Fact]
    public void A_row_with_no_grant_command_offers_no_more_tries()
    {
        var form = Form(Paused() with { Cli = "sindri workflow answer ITM-01JC8/step/1 --text <text>" });

        form.Choose(PausedAnswerOption.MoreTries);

        Assert.Equal((false, PausedAnswerOption.Hint), (form.ShowsMoreTries, form.Option));
    }

    [Fact]
    public async Task A_refused_answer_shows_the_served_words_and_can_be_pressed_again()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-041", "That run has moved on. Read the instance again.");
        var form = Form();
        form.Choose(PausedAnswerOption.Stop);

        await form.Send();

        Assert.Equal(("That run has moved on. Read the instance again.", false, true), (form.Refusal, form.IsSent, form.CanSend));
    }
}
