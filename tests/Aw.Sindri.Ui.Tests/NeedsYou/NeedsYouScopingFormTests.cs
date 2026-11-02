using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The scoping interview's question form (q65 d), bound to the interview goldens: a choice per question among its answers, Other or Skip; Send answers waits for every pick; every round appends one batch to "fact:answers".</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouScopingFormTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();

    private async Task<NeedsYouBarViewModel> Bar(string? answersSoFar = null)
    {
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "scoping-interview"));
        _server.FactBody = (_, key) => key switch
        {
            "fact:questions" => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance-fact", "scoping-questions")),
            "fact:answers" when answersSoFar is not null => new RouteAnswer(HttpStatusCode.OK, $$"""{"key":"fact:answers","type":"payload","body":{{answersSoFar}},"truncated":false}"""),
            _ => null,
        };
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), _clock);
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk(), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        return bar;
    }

    private static JsonNode Served() => GoldenFiles.Node("workflow-instance-fact", "scoping-questions")["body"]!;

    private static void AnswerAll(NeedsYouBarViewModel bar)
    {
        foreach (var question in bar.Scoping)
        {
            question.Choices[0].Choose();
        }
    }

    [Fact]
    public async Task Each_golden_question_is_a_choice_among_its_answers_then_Other_then_Skip()
    {
        var bar = await Bar();

        Assert.True(bar.HasScoping);
        Assert.False(bar.HasQuestions);   // the design review's form is not the one shown
        var served = Served().AsArray();
        Assert.Equal(served.Count, bar.Scoping.Count);
        var first = bar.Scoping[0];
        var options = served[0]!["options"]!.AsArray();
        Assert.Equal(options.Select(o => o!["answer"]!.GetValue<string>()).Concat(["Other", "Skip"]), first.Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Equal(options.Select(o => o!["rationale"]!.GetValue<string>()), first.Choices.Take(options.Count).Select(c => c.Rationale), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Exactly_one_answer_per_question_is_marked_recommended()
    {
        var bar = await Bar();

        Assert.All(bar.Scoping, q => Assert.Single(q.Choices, c => c.IsRecommended));
    }

    [Fact]
    public async Task The_recommended_answer_is_the_option_the_agent_marked_with_recommended_true()
    {
        var bar = await Bar();

        var served = Served().AsArray();
        for (var i = 0; i < served.Count; i++)
        {
            var marked = served[i]!["options"]!.AsArray().Single(o => o!["recommended"]?.GetValue<bool>() == true)!["id"]!.GetValue<string>();
            Assert.Equal(marked, bar.Scoping[i].Choices.Single(c => c.IsRecommended).OptionId);   // #1204: a boolean on each option of fact:questions, shown as the tag
        }
    }

    [Fact]
    public async Task Send_answers_waits_for_every_question_to_have_a_pick()
    {
        var bar = await Bar();

        Assert.Equal(("Send answers", false), (bar.ApproveWord, bar.PrimaryEnabled));
        foreach (var question in bar.Scoping.Take(bar.Scoping.Count - 1))
        {
            question.Choices[0].Choose();
        }

        Assert.False(bar.PrimaryEnabled);
        bar.Scoping[^1].Choices[^1].Choose();   // Skip counts as a pick

        Assert.True(bar.PrimaryEnabled);
        Assert.False(bar.ShowsSendBack);
    }

    [Fact]
    public async Task Other_needs_its_words_before_it_counts()
    {
        var bar = await Bar();
        AnswerAll(bar);
        var other = bar.Scoping[0].Choices.Single(c => c.Kind == ScopingChoiceKind.Other);

        other.Choose();

        Assert.Equal((true, false, false), (bar.Scoping[0].IsOther, bar.Scoping[0].IsAnswered, bar.PrimaryEnabled));
        bar.Scoping[0].OtherText = "  links expire after 90 days  ";
        Assert.True(bar.PrimaryEnabled);
    }

    [Fact]
    public async Task A_pick_replaces_the_one_before_it()
    {
        var bar = await Bar();
        var question = bar.Scoping[0];

        question.Choices[0].Choose();
        question.Choices[1].Choose();

        Assert.Equal([false, true], question.Choices.Take(2).Select(c => c.IsChosen));
    }

    [Fact]
    public async Task Round_one_writes_the_whole_array_with_one_batch_of_the_picks()
    {
        var bar = await Bar();
        var served = Served().AsArray();
        bar.Scoping[0].Choices[1].Choose();                                                  // an offered answer
        bar.Scoping[1].Choices.Single(c => c.Kind == ScopingChoiceKind.Other).Choose();
        bar.Scoping[1].OtherText = "from the date they were made";                           // Other, with words
        bar.Scoping[2].Choices[^1].Choose();                                                  // Skip

        await bar.PressApprove();

        var sent = Assert.Single(_server.CompleteCalls).Body;
        Assert.Equal("answered", sent.Outcome);
        var batches = JsonNode.Parse(Encoding.UTF8.GetString(sent.Outputs["fact:answers"]))!.AsArray();
        var batch = Assert.Single(batches)!;
        Assert.Equal((1, "owner"), (batch["round"]!.GetValue<int>(), batch["actor"]!.GetValue<string>()));
        Assert.StartsWith("2026-", batch["at"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.EndsWith("Z", batch["at"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(served.Select(q => q!["id"]!.GetValue<string>()), batch["questionIds"]!.AsArray().Select(i => i!.GetValue<string>()), StringComparer.Ordinal);
        var answers = batch["answers"]!.AsArray();
        var second = served[0]!["options"]![1]!;
        Assert.Equal((served[0]!["id"]!.GetValue<string>(), second["id"]!.GetValue<string>(), second["answer"]!.GetValue<string>()), (answers[0]!["question"]!.GetValue<string>(), answers[0]!["option"]!.GetValue<string>(), answers[0]!["text"]!.GetValue<string>()));
        Assert.Equal("from the date they were made", answers[1]!["other"]!.GetValue<string>());
        Assert.True(answers[2]!["skip"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Round_two_appends_a_batch_and_leaves_the_first_alone()
    {
        const string RoundOne = """[{"round":1,"actor":"owner","at":"2026-10-12T09:00:00.0000000Z","questionIds":["Q1"],"answers":[{"question":"Q1","option":"Q1.a","text":"kept"}]}]""";
        var bar = await Bar(RoundOne);
        AnswerAll(bar);

        await bar.PressApprove();

        var batches = JsonNode.Parse(Encoding.UTF8.GetString(Assert.Single(_server.CompleteCalls).Body.Outputs["fact:answers"]))!.AsArray();
        Assert.Equal(2, batches.Count);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(RoundOne)![0], batches[0]));   // the first batch is written back as it was
        Assert.Equal(2, batches[1]!["round"]!.GetValue<int>());
    }

    [Fact]
    public async Task Done_asking_sends_what_is_answered_so_far_with_its_own_outcome()
    {
        var bar = await Bar();
        bar.Scoping[0].Choices[0].Choose();

        Assert.False(bar.PrimaryEnabled);
        Assert.True(bar.DoneAskingEnabled);
        await bar.PressDoneAsking();

        var sent = Assert.Single(_server.CompleteCalls).Body;
        Assert.Equal("doneAsking", sent.Outcome);
        var batch = JsonNode.Parse(Encoding.UTF8.GetString(sent.Outputs["fact:answers"]))!.AsArray().Single()!;
        _ = Assert.Single(batch["answers"]!.AsArray());
        Assert.Equal(Served().AsArray().Count, batch["questionIds"]!.AsArray().Count);   // every shown question is named, only the answered ones have an answer
    }

    [Fact]
    public async Task Answers_that_cannot_be_read_are_never_written_over()
    {
        var bar = await Bar(answersSoFar: """{"not":"a list"}""");
        AnswerAll(bar);

        await bar.PressApprove();

        Assert.Empty(_server.CompleteCalls);
        Assert.True(bar.HasRefusal);
    }

    [Fact]
    public async Task The_design_reviews_words_as_options_keep_the_q60_form()
    {
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "lens-ask-multi"));
        _server.FactBody = (_, key) => string.Equals(key, "fact:escalation", StringComparison.Ordinal)
            ? new RouteAnswer(HttpStatusCode.OK, """{"key":"fact:escalation","type":"payload","body":[{"id":"q1","question":"Keep it?","options":["Yes","No"]}],"truncated":false}""")
            : null;
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), _clock);
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk(), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;

        Assert.Equal((true, false), (bar.HasQuestions, bar.HasScoping));
    }
}
