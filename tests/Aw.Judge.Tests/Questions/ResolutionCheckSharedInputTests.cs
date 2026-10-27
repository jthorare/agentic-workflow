using System.Security.Cryptography;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

/// <summary>
/// The input bugfix-state writes for resolution-check (tests/fixtures/judge-input/resolution-check.json, a real one with
/// every field, the failure lines included) is digested to the 16 hex the helper computes from the same bytes. The helper
/// refuses a verdict about any other digest, so a field the judge's record drops fails every call (#1290).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ResolutionCheckSharedInputTests
{
    private static readonly byte[] _shared = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "JudgeInput", "resolution-check.json"));

    [Fact]
    public async Task The_judge_stores_the_digest_the_helper_computes_from_the_bytes_it_wrote()
    {
        var run = new QuestionRun();

        _ = await run.Ask(ResolutionCheckQuestion.Definition, System.Text.Encoding.UTF8.GetString(_shared), Cli(ContentClass.Brief).Decides("resolved", 1));

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(_shared))[..16], Assert.Single(run.Log.Decisions).InputDigest);
    }

    [Fact]
    public async Task The_red_run_s_failure_lines_reach_the_prompt_fenced_as_untrusted()
    {
        var cli = Cli(ContentClass.Brief).Decides("resolved", 1);

        _ = await new QuestionRun().Ask(ResolutionCheckQuestion.Definition, System.Text.Encoding.UTF8.GetString(_shared), cli);

        var prompt = Assert.Single(cli.Calls).Question.Prompt;
        Assert.Contains("<baseline-failure>", prompt, StringComparison.Ordinal);
        var fenced = prompt[prompt.IndexOf("<baseline-failure>", StringComparison.Ordinal)..prompt.IndexOf("</baseline-failure>", StringComparison.Ordinal)];
        Assert.Contains("failed Aw.Sindri.Tests.Scoping.ScopeCoverageDemotedLensNodesTests.A_demoted_file_next_to_an_anchor_raises_no_COV_003", fenced, StringComparison.Ordinal);
    }
}
