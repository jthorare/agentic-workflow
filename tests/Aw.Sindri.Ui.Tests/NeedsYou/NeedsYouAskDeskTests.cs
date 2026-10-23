using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The design review's ask desk (engine #817): every question of "fact:escalation" with its own answer field, options as choices, every answer needed, and the answers sent once as [{id, question, answer}].</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouAskDeskTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();

    private static string Fact(string body) => $$"""{"key":"fact:escalation","type":"payload","body":{{body}},"truncated":false}""";

    private const string ThreeQuestions =
        """[{"id":"q1","kind":"productQuestion","question":"Keep the legacy flow?","options":["Keep it","Drop it"],"lens":"product","finding":"F-1"},""" +
        """{"id":"q2","kind":"ambiguousFix","question":"Which table owns expiry?","lens":"data"},""" +
        """{"id":"q3","kind":"ambiguousFix","question":"Who is the expiry for?"}]""";

    // The lens-ask-multi golden: the escalation fact is listed as an input; its full list comes from the fact read.
    private async Task<NeedsYouBarViewModel> Bar(string escalationBody)
    {
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "lens-ask-multi"));
        _server.FactBody = (_, key) => string.Equals(key, "fact:escalation", StringComparison.Ordinal) ? new RouteAnswer(HttpStatusCode.OK, Fact(escalationBody)) : null;
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), new FakeClipboard(), _clock);
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk(), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        return bar;
    }

    [Fact]
    public async Task An_array_of_questions_gives_one_field_each_in_the_order_served()
    {
        var bar = await Bar(ThreeQuestions);

        Assert.True(bar.HasQuestions);
        Assert.Equal(["Keep the legacy flow?", "Which table owns expiry?", "Who is the expiry for?"], bar.Questions.Select(q => q.Prompt), StringComparer.Ordinal);
        Assert.Equal(["From the product review", "From the data review", ""], bar.Questions.Select(q => q.Context), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_single_object_is_one_question()
    {
        var bar = await Bar("""{"question":"Ship it?","options":["Yes","No"]}""");

        var only = Assert.Single(bar.Questions);
        Assert.Equal(("q1", "Ship it?", true), (only.Question.Id, only.Prompt, only.HasOptions));
    }

    [Fact]
    public async Task Options_are_choices_that_fill_the_answer()
    {
        var bar = await Bar(ThreeQuestions);
        var first = bar.Questions[0];

        Assert.Equal(["Keep it", "Drop it"], first.Options.Select(o => o.Text), StringComparer.Ordinal);
        Assert.False(bar.Questions[1].HasOptions);

        first.Options[1].Choose();

        Assert.Equal(("Drop it", true), (first.Answer, first.IsAnswered));
    }

    [Fact]
    public async Task Every_answer_is_needed_before_the_answers_can_be_sent()
    {
        var bar = await Bar(ThreeQuestions);

        Assert.Equal(("Send answers", false, "Answer every question to send."), (bar.ApproveWord, bar.PrimaryEnabled, bar.QuestionsHint));
        bar.Questions[0].Answer = "Keep it";
        bar.Questions[1].Answer = "   ";
        Assert.False(bar.PrimaryEnabled);
        await bar.PressApprove();
        Assert.Empty(_server.CompleteCalls);

        bar.Questions[1].Answer = "expiry_rules";
        bar.Questions[2].Answer = "the tenant admin";

        Assert.Equal((true, true, ""), (bar.AllAnswered, bar.PrimaryEnabled, bar.QuestionsHint));
        Assert.False(bar.ShowsSendBack);   // the ask step has nothing to send back
    }

    [Fact]
    public async Task The_answers_are_sent_once_as_id_question_answer_in_order()
    {
        var bar = await Bar(ThreeQuestions);
        bar.Questions[0].Options[0].Choose();
        bar.Questions[1].Answer = " expiry_rules ";
        bar.Questions[2].Answer = "the tenant admin";

        await bar.PressApprove();

        var sent = Assert.Single(_server.CompleteCalls).Body;
        Assert.Equal("done", sent.Outcome);
        var answers = JsonNode.Parse(Encoding.UTF8.GetString(sent.Outputs["fact:answers"]))!.AsArray();
        Assert.Equal(
            [("q1", "Keep the legacy flow?", "Keep it"), ("q2", "Which table owns expiry?", "expiry_rules"), ("q3", "Who is the expiry for?", "the tenant admin")],
            answers.Select(a => (a!["id"]!.GetValue<string>(), a["question"]!.GetValue<string>(), a["answer"]!.GetValue<string>())));
    }

    [Fact]
    public async Task A_desk_that_asks_nothing_keeps_its_approve_and_send_back()
    {
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"));
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), new FakeClipboard(), _clock);
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk(), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;

        Assert.Equal((false, "Approve", true), (bar.HasQuestions, bar.ApproveWord, bar.ShowsSendBack));
    }
}
