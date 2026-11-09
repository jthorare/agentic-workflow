using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class AskOwnerBlockTests
{
    private static readonly DateTimeOffset _now = new(2026, 3, 7, 12, 0, 0, TimeSpan.Zero);

    private static JsonObject Config() => new()
    {
        ["question"] = "Ship the release?",
        ["choices"] = new JsonArray("yes", "no"),
        ["timeout"] = "2h",
        ["default"] = "no",
    };

    [Fact]
    public async Task The_block_posts_the_question_with_its_choices_default_and_deadline()
    {
        var port = new BlockFakes.RecordingQuestionPort();

        var result = await new AskOwnerBlock(port, new FakeTimeProvider(_now)).Execute(BlockFakes.Context(Config(), node: "ask"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var posted = Assert.Single(port.Posted);
        Assert.Equal(("wf@1:1", "ask", "Ship the release?", "no", (DateTimeOffset?)_now.AddHours(2)), (posted.Run.Value, posted.Node.Value, posted.Question, posted.Default, posted.TimeoutAt));
        Assert.Equal(["yes", "no"], posted.Choices);
    }

    [Fact]
    public async Task A_question_the_port_cannot_write_fails_the_node_with_the_ports_reason()
    {
        var port = new BlockFakes.RecordingQuestionPort { Fail = KernelErrors.Invalid.With("the ledger is read-only") };

        var result = await new AskOwnerBlock(port, new FakeTimeProvider(_now)).Execute(BlockFakes.Context(Config()), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("the ledger is read-only", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unanswered_question_takes_the_default_branch_at_its_timeout()
    {
        var outcome = AskOwnerBlock.Resolve(Config(), answer: null, answeredBy: null);

        Assert.Equal("timeout", outcome.Branch);
        Assert.Equal("no", outcome.Outputs["answer"]!.GetValue<string>());
        Assert.Equal("timeout", outcome.Outputs["answeredBy"]!.GetValue<string>());
    }

    [Fact]
    public void An_answer_before_the_timeout_wins_over_the_default()
    {
        var outcome = AskOwnerBlock.Resolve(Config(), "yes", "owner");

        Assert.Equal("answered", outcome.Branch);
        Assert.Equal("yes", outcome.Outputs["answer"]!.GetValue<string>());
        Assert.Equal("owner", outcome.Outputs["answeredBy"]!.GetValue<string>());
    }

    [Fact]
    public void A_timeout_with_no_default_answers_with_empty_text()
    {
        var config = Config();
        _ = config.Remove("default");

        var outcome = AskOwnerBlock.Resolve(config, answer: null, answeredBy: null);

        Assert.Equal("timeout", outcome.Branch);
        Assert.Equal(string.Empty, outcome.Outputs["answer"]!.GetValue<string>());
    }

    [Fact]
    public void The_timeout_and_choices_are_read_from_the_config()
    {
        Assert.Equal(TimeSpan.FromHours(2), AskOwnerBlock.TimeoutOf(Config()));
        Assert.Null(AskOwnerBlock.TimeoutOf(new JsonObject { ["question"] = "Why?" }));
        Assert.Equal(["yes", "no"], AskOwnerBlock.ChoicesOf(Config()));
        Assert.Empty(AskOwnerBlock.ChoicesOf(new JsonObject { ["question"] = "Why?" }));
    }

    [Theory]
    [InlineData("yes", null, null)]
    [InlineData(null, "no", null)]
    [InlineData("maybe", null, "is not one of")]
    [InlineData(null, "maybe", "is not one of")]
    [InlineData(null, null, "needs an answer")]
    [InlineData("yes", "yes", "only one")]
    public void An_answer_must_be_one_of_the_choices_when_the_node_offers_any(string? text, string? choice, string? problem)
    {
        var checkedAgainst = AskOwnerBlock.CheckAnswer(["yes", "no"], text, choice);

        if (problem is null)
        {
            Assert.Null(checkedAgainst);
        }
        else
        {
            Assert.Contains(problem, checkedAgainst, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Any_text_is_an_answer_when_the_node_offers_no_choices_and_a_choice_is_not()
    {
        Assert.Null(AskOwnerBlock.CheckAnswer([], "ship it on Friday", choice: null));
        Assert.Contains("no choices", AskOwnerBlock.CheckAnswer([], text: null, "yes"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_ask_owner_block_declares_its_config_outputs_branches_and_a_valid_example()
    {
        IGraphBlockType block = new AskOwnerBlock(new BlockFakes.RecordingQuestionPort(), new FakeTimeProvider(_now));
        var example = JsonNode.Parse(block.Example)!.AsObject();

        Assert.Equal("ask_owner", block.Type);
        Assert.False(block.IsTrigger);
        Assert.Empty(block.Config.Check(example));
        Assert.Empty(block.CheckConfig(example));
        var outputs = block.OutputsFor(example);
        Assert.Equal(["answer", "answeredBy"], outputs.Fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["answered", "timeout"], outputs.BranchNames);
    }

    [Fact]
    public void A_default_that_is_not_a_choice_and_a_bad_timeout_are_refused()
    {
        IGraphBlockType block = new AskOwnerBlock(new BlockFakes.RecordingQuestionPort(), new FakeTimeProvider(_now));
        var config = Config();
        config["default"] = "maybe";
        config["timeout"] = "soon";

        var problems = block.CheckConfig(config);

        Assert.Contains(problems, p => p.Field == "default");
        Assert.Contains(problems, p => p.Field == "timeout");
    }
}
