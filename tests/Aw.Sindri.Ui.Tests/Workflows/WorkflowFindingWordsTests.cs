using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A validation finding reads in product words (#4397): the step by its card title, no node ids, no code words, one term for "to fix".</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowFindingWordsTests
{
    private static readonly string[] _codes =
    [
        "GRAPH-AGENT-001", "GRAPH-AGENT-002", "GRAPH-BLOCK-001", "GRAPH-CODE-001", "GRAPH-COND-001", "GRAPH-CONFIG-001", "GRAPH-CRON-001", "GRAPH-CYCLE-001",
        "GRAPH-EDGE-001", "GRAPH-EDGE-002", "GRAPH-LOOP-001", "GRAPH-LOOP-002", "GRAPH-LOOP-003", "GRAPH-NODE-001", "GRAPH-NODE-002", "GRAPH-NODE-003",
        "GRAPH-NODE-004", "GRAPH-NODE-005", "GRAPH-PROMPT-001", "GRAPH-REF-001", "GRAPH-REF-002", "GRAPH-REF-003", "GRAPH-REF-004", "GRAPH-REF-005",
        "GRAPH-RESULT-001", "GRAPH-TRIGGER-001", "GRAPH-TRIGGER-002", "GRAPH-WIRING-001",
    ];

    private static readonly string[] _codeWords = ["node", "trigger", "{{", "}}", "'", "config.", "edge", "GRAPH-", "onFailure", "for_each", "id "];

    public static TheoryData<string> Codes() => [.. _codes];

    [Theory]
    [MemberData(nameof(Codes))]
    public void Finding_text_has_no_code_words(string code)
    {
        var (sentence, fix) = WorkflowFindingWords.Say(code, "Prompt", "config.prompt");

        Assert.All(_codeWords, w => Assert.DoesNotContain(w, sentence + " " + fix, StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(fix);
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void Finding_names_the_step_by_its_card_title(string code)
    {
        var (sentence, _) = WorkflowFindingWords.Say(code, "Prompt", field: null);

        Assert.Contains("Prompt", sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_prompt_that_reads_no_data_says_what_to_add()
    {
        var (sentence, fix) = WorkflowFindingWords.Say("GRAPH-PROMPT-001", "Prompt", "config.prompt");

        Assert.Equal(("Prompt has nothing to read: no step before it fetches data.", "Add a step before it, then use that step's output in the question."), (sentence, fix));
    }

    [Fact]
    public void An_unmapped_code_falls_back_to_a_product_sentence_with_the_title()
    {
        var (sentence, fix) = WorkflowFindingWords.Say("GRAPH-NEW-999", "Prompt", field: null);

        Assert.Equal(("Prompt has something to fix.", "Open the step and check its settings."), (sentence, fix));
    }

    [Fact]
    public void A_finding_about_the_whole_workflow_names_the_workflow()
    {
        var (sentence, _) = WorkflowFindingWords.Say("GRAPH-TRIGGER-002", WorkflowFindingWords.Workflow, field: null);

        Assert.StartsWith("This workflow", sentence, StringComparison.Ordinal);
    }
}
