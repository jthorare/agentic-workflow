using System.Text;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Features.AskQuestion;
using Aw.Judge.Host;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.AskQuestion.AskQuestionHarness;

namespace Aw.Judge.Tests.AskQuestion;

// choose's prompt facts share the providers' input cap (config.json providers.inputCapChars, default 160,000); every other question keeps its own caps (#1447).
[Lane(TestLane.Unit)]
public sealed class ChooseInputCapTests
{
    private const string Choices = """[{"name":"ui","criterion":"only UI files changed"},{"name":"api","criterion":"an HTTP route changed"}]""";

    private readonly AskQuestionHarness _harness = new();

    [Fact]
    public async Task A_60_KB_brief_fact_reaches_the_choose_prompt_whole()
    {
        var cli = Cli(ContentClass.Brief).Decides("ui", 0.9);

        _ = await _harness.Ask(new AskQuestionRequest("choose", ChooseInput(new string('b', 60_000), "the entry fact")), cli);

        var prompt = Assert.Single(cli.Calls).Question.Prompt;
        Assert.Equal(60_000, LongestRun(prompt, 'b'));
        Assert.DoesNotContain("[truncated", prompt, StringComparison.Ordinal);
        Assert.Contains("the entry fact", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Over_the_cap_only_the_longest_fact_is_cut()
    {
        _harness.Config.Stored = Parse("""{"questions":{},"providers":{"inputCapChars":20000}}""");
        var cli = Cli(ContentClass.Brief).Decides("ui", 0.9);

        _ = await _harness.Ask(new AskQuestionRequest("choose", ChooseInput(new string('b', 60_000), new string('e', 500))), cli);

        var prompt = Assert.Single(cli.Calls).Question.Prompt;
        Assert.Equal(500, LongestRun(prompt, 'e'));
        Assert.Contains("[truncated", prompt, StringComparison.Ordinal);
        Assert.InRange(LongestRun(prompt, 'b'), 8_001, 20_000);
        Assert.True(prompt.Length <= 20_000 + 2_000, $"the prompt is {prompt.Length} chars");
    }

    [Fact]
    public void A_question_other_than_choose_still_caps_its_brief_at_8000()
    {
        var input = new ResolutionCheckInput(new string('b', 60_000), "e", "a", "r", CheckKind.Test, "s", BeforePassed: false, AfterPassed: true, "stat");

        var prompt = ResolutionCheckQuestion.Definition.ToRef(input).Prompt;

        Assert.Equal(8_000, LongestRun(prompt, 'b'));
        Assert.Contains("[truncated 52000 chars]", prompt, StringComparison.Ordinal);
    }

    private static string ChooseInput(string brief, string entry) =>
        $$"""{"prompt":"Which evidence?","state":{"brief":"{{brief}}","entry":"{{entry}}"},"choices":{{Choices}}}""";

    private static int LongestRun(string text, char c)
    {
        int longest = 0, run = 0;
        foreach (var ch in text)
        {
            run = ch == c ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    private static JudgeConfig Parse(string text) =>
        JudgeConfigParser.Parse(Encoding.UTF8.GetBytes(text), AwJson.CreateOptions(JudgeJsonContext.Default)).Value;
}
