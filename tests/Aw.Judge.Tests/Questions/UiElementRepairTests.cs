using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class UiElementRepairTests
{
    [Fact]
    public async Task No_candidates_is_no_good_candidate_without_a_model()
    {
        var cli = Cli(ContentClass.Code).Decides("repaired", 1);

        var outcome = await new QuestionRun().Ask(UiElementRepairQuestion.Definition, """{"brokenSelector":"#save","step":"click save","candidates":[]}""", cli);

        AssertPreRule("no-good-candidate", outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task The_candidates_reach_the_prompt_and_the_chosen_index_comes_back()
    {
        var extra = new System.Text.Json.Nodes.JsonObject { ["chosenIndex"] = 1 };
        var cli = Cli(ContentClass.Code).Returns(new ProviderDecided("repaired", 0.9, "model") { Extra = extra });
        const string Input = """{"brokenSelector":"#save","step":"click save","candidates":[{"index":0,"role":"link","accessibleName":"Home","testId":null,"text":"Home"},{"index":1,"role":"button","accessibleName":"Save","testId":"save-btn","text":null}]}""";

        var outcome = await new QuestionRun().Ask(UiElementRepairQuestion.Definition, Input, cli);

        Assert.Equal("""{"chosenIndex":1}""", AssertModel("repaired", outcome).Extra?.ToJsonString());
        var prompt = Assert.Single(cli.Calls).Question.Prompt;
        Assert.Contains("[0] role=link name=Home testId=? text=Home", prompt, StringComparison.Ordinal);
        Assert.Contains("[1] role=button name=Save testId=save-btn text=?", prompt, StringComparison.Ordinal);
    }
}
