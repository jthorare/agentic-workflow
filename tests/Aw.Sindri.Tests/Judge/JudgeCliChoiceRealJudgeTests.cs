using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Infrastructure.Judge;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Judge;

/// <summary>
/// The adapter against the real judge program: the one AW_SINDRI_JUDGE names (the C# judge host; the TS judge on the PATH has no 'choose').
/// It skips when that program is absent or doesn't know 'choose', and only offers the 'rules' provider, so it never spends an agent call.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class JudgeCliChoiceRealJudgeTests
{
    private static readonly string[] _rulesOnly = ["rules"];

    private static readonly ChooseOption[] _options = [new("small", "touches one file"), new("large", "touches many files")];

    private static JudgeCliChoice Port()
    {
        var program = Environment.GetEnvironmentVariable("AW_SINDRI_JUDGE");
        Assert.SkipWhen(string.IsNullOrEmpty(program) || !File.Exists(program), "AW_SINDRI_JUDGE does not name a judge program (set it to the C# judge to run this)");
        var state = Directory.CreateTempSubdirectory("aw-judge-real-").FullName;
        return new JudgeCliChoice(new ProcessToolRunner(), static name => Environment.GetEnvironmentVariable(name), state);
    }

    private static ChooseRequest Request(string prompt) => new(prompt, _options, new Dictionary<string, string>(StringComparer.Ordinal) { ["brief"] = "rename one variable" }, 0.6, _rulesOnly);

    [Fact]
    public async Task With_only_the_rules_provider_a_choose_call_escalates_with_a_decision_id()
    {
        var answer = await Port().Choose(Request("Which size is this change?"), TestContext.Current.CancellationToken);

        Assert.SkipWhen(answer.Status == ChooseStatus.Failed && answer.Reason.Contains("unknown question", StringComparison.Ordinal), "the program does not know 'choose'");
        Assert.Equal(ChooseStatus.Escalated, answer.Status);
        Assert.Equal("no-provider-decided", answer.Reason);
        Assert.False(string.IsNullOrEmpty(answer.DecisionRef));
    }

    [Fact]
    public async Task An_empty_prompt_is_refused_by_the_real_judge_as_a_failure()
    {
        var answer = await Port().Choose(Request(string.Empty), TestContext.Current.CancellationToken);

        Assert.SkipWhen(answer.Status == ChooseStatus.Failed && answer.Reason.Contains("unknown question", StringComparison.Ordinal), "the program does not know 'choose'");
        Assert.Equal(ChooseStatus.Failed, answer.Status);
        Assert.Equal("invalid-input", answer.Reason);
    }
}
