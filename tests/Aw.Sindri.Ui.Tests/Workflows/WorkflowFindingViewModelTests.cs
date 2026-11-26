using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A finding binds its words to the step's card title (#4397): a rename changes what a bound view shows, and the codes whose own text carries what is wrong keep the server's message under Details.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowFindingViewModelTests
{
    private static WorkflowFindingViewModel Finding(string code, string message = "the server's text", string? field = "config.prompt") =>
        new(new WorkflowFindingView(code, "error", ["agent"], field, message, "the server's fix", code + ":agent", "agent", message));

    [Fact]
    public void Renaming_the_step_raises_a_change_for_the_sentence_the_fix_line_and_the_name()
    {
        var finding = Finding("GRAPH-PROMPT-001");
        finding.StepTitle = "Prompt";
        var changed = new List<string?>();
        finding.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        finding.StepTitle = "Ask the model";

        Assert.Equal(["FixLine", "Name", "Sentence"], changed.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        Assert.StartsWith("Ask the model", finding.Sentence, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GRAPH-CODE-001")]
    [InlineData("GRAPH-CRON-001")]
    [InlineData("GRAPH-COND-001")]
    [InlineData("GRAPH-REF-002")]
    [InlineData("GRAPH-CONFIG-001")]
    [InlineData("GRAPH-REPO-001")]
    [InlineData("GRAPH-NAME-001")]
    public void A_code_whose_own_text_says_what_is_wrong_keeps_the_servers_message_under_Details(string code)
    {
        var finding = Finding(code, message: "line 3: CS1002 ; expected");

        Assert.Equal((true, "line 3: CS1002 ; expected"), (finding.HasDetails, finding.Details));
    }

    [Fact]
    public void A_code_the_words_cover_completely_has_no_Details()
    {
        var finding = Finding("GRAPH-PROMPT-001");

        Assert.Equal((false, string.Empty), (finding.HasDetails, finding.Details));
    }
}
