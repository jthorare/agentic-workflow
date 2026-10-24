using Aw.Evidence.Core.Judging;
using Aw.Evidence.Infrastructure.Judging;
using Aw.Evidence.Infrastructure.Processes;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// The adapter against the real C# judge, offline (spec §12 unit 1.3): AW_JUDGE_BIN names a built judge, AW_JUDGE_PROVIDERS is
/// rules, and the state is a scratch directory, never the real one. Skips unless AW_JUDGE_BIN is set.
/// </summary>
public sealed class JudgeCliJudgeRealTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task The_real_judge_decides_from_its_rules_offline_and_escalates_what_rules_cannot_decide()
    {
        var bin = Environment.GetEnvironmentVariable("AW_JUDGE_BIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(bin) && Path.IsPathRooted(bin), "AW_JUDGE_BIN names no built judge");
        var scratch = Directory.CreateTempSubdirectory("aw-judge-real-");
        try
        {
            var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["AW_JUDGE_BIN"] = bin,
                ["AW_JUDGE_PROVIDERS"] = "rules",
                ["AW_STATE_DIR"] = scratch.FullName,
                ["DOTNET_ROOT"] = Environment.GetEnvironmentVariable("DOTNET_ROOT"),   // a framework-dependent judge build needs it
            };
            var judge = new JudgeCliJudge(new ProcessCliRunner(TimeSpan.FromSeconds(30)), new FileExecutableLocator(), name => settings.GetValueOrDefault(name));

            var repair = await judge.RepairSelectorAsync(new SelectorRepairRequest("open-panel", "click open-panel", []), CancellationToken.None);
            var critique = await judge.CritiqueAsync(new VisualCritiqueRequest(Path.Combine(scratch.FullName, "after.png"), BaselineScreenshot: null, scratch.FullName), CancellationToken.None);

            Assert.True(judge.IsAvailable);
            Assert.NotNull(repair);
            Assert.Equal((RepairDecision.NoGoodCandidate, null), (repair.Decision, repair.ChosenIndex));
            Assert.Equal("rules", repair.Usage?.Model);
            Assert.Null(critique);   // no rule judges a screenshot and no model is allowed: the judge escalates, the verdict stays unchecked
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }
}
