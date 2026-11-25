using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A validation finding reads in product words (#4397): the step by its card title, no node ids, no code words, one term for "to fix".</summary>
[Lane(TestLane.Unit)]
public sealed partial class WorkflowFindingWordsTests
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

        Assert.Equal(("Prompt has nothing to read: no step before it fetches data.", "Add a step before it, then use the output of that step in the question."), (sentence, fix));
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

    [GeneratedRegex("\"(?<code>GRAPH-[A-Z]+-[0-9]{3})\"", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FullCode { get; }

    [GeneratedRegex("\"(?<code>GRAPH-[A-Z]+)\"", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Prefix { get; }

    // The codes the validator can emit: every literal in its source, and the REPO and NAME prefixes it completes with -001 and -002 (GraphValidator.Nodes.cs, Unanswered). A new code that reaches the app with no row fails here.
    private static IReadOnlyList<string> ValidatorCodes()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AgenticWorkflow.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("no AgenticWorkflow.slnx above the test binaries");
        }

        var codes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "Sindri", "Aw.Sindri.Core", "Graph", "Validation"), "*.cs"))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in FullCode.Matches(text))
            {
                _ = codes.Add(m.Groups["code"].Value);
            }

            foreach (Match m in Prefix.Matches(text))
            {
                _ = codes.Add(m.Groups["code"].Value + "-001");
                _ = codes.Add(m.Groups["code"].Value + "-002");
            }
        }

        return [.. codes];
    }

    [Fact]
    public void Every_code_the_validator_can_emit_has_a_row()
    {
        var missing = ValidatorCodes().Where(static c => !WorkflowFindingWords.Knows(c)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void The_fallback_fix_for_a_finding_about_the_whole_workflow_does_not_send_the_person_to_a_step()
    {
        var (_, fix) = WorkflowFindingWords.Say("GRAPH-NEW-999", WorkflowFindingWords.Workflow, field: null);

        Assert.DoesNotContain("step", fix, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("GRAPH-TRIGGER-002")]
    [InlineData("GRAPH-RESULT-001")]
    public void A_whole_workflow_finding_reads_as_a_sentence_about_the_workflow(string code)
    {
        var (sentence, _) = WorkflowFindingWords.Say(code, WorkflowFindingWords.Workflow, field: null);

        Assert.False(sentence.StartsWith("This workflow is ", StringComparison.Ordinal));
        Assert.StartsWith("This workflow ", sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_setting_that_is_wrong_names_which_setting()
    {
        var (sentence, fix) = WorkflowFindingWords.Say("GRAPH-CONFIG-001", "Wait", "config.duration");

        Assert.Equal(("Wait has a wrong duration setting.", "Open the step and correct the duration setting."), (sentence, fix));
    }

    [Fact]
    public void The_words_say_exit_and_never_way_out()
    {
        var all = string.Join(' ', ValidatorCodes().Select(static c => WorkflowFindingWords.Say(c, "Step", field: null).Sentence + " " + WorkflowFindingWords.Say(c, "Step", field: null).Fix));

        Assert.DoesNotContain("way out", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ways out", all, StringComparison.OrdinalIgnoreCase);
    }
}
