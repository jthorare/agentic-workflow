using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Skills;

/// <summary>The host fetches a skill Step's PR head into the run's own refs before the launch (skill flows spec §3.1), from a local origin that carries 'refs/pull/7/head'.</summary>
[Lane(TestLane.Integration)]
public sealed class PrHeadFetchTests : IAsyncLifetime
{
    private static readonly InstanceAddress _instance = InstanceAddress.From("ITM-00000000000000000000000000000001/review/1");
    private TempGitRepo _origin = null!;
    private TempGitRepo _work = null!;
    private string _prSha = string.Empty;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _origin = await TempGitRepo.Create();
        _ = await _origin.Git("checkout", "-q", "-b", "feature");
        _ = await _origin.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "--allow-empty", "-m", "the PR's change");
        _prSha = await _origin.Git("rev-parse", "HEAD");
        _ = await _origin.Git("update-ref", "refs/pull/7/head", _prSha);
        _ = await _origin.Git("checkout", "-q", "main");
        _work = await TempGitRepo.Create();
        _ = await _work.Git("remote", "add", "origin", _origin.Path);
    }

    public ValueTask DisposeAsync()
    {
        _origin.Dispose();
        _work.Dispose();
        return ValueTask.CompletedTask;
    }

    private LaunchRequest Request(string? pr) =>
        A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("review", "agent"))).OnInstance(_instance, RunToken.For(1, 1)).Build() with
        {
            RepoPath = _work.Path,
            Skill = pr is null ? null : new SkillLaunch("review", SkillTarget.ClaudeCode, SkillFixtures.Review, new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = pr }),
        };

    [Fact]
    public async Task The_PR_head_lands_on_a_branch_of_the_runs_namespace()
    {
        var fetched = await PrHeadFetch.For(Request("7"), Token);

        Assert.True(fetched.IsSuccess);
        Assert.Equal(_prSha, fetched.Value!.Head);
        Assert.Equal(_prSha, await _work.Git("rev-parse", "refs/heads/" + fetched.Value.Branch));
        Assert.Matches("^sindri/.+/1/pr$", fetched.Value.Branch);
    }

    [Fact]
    public async Task A_PR_link_is_fetched_like_its_number()
    {
        var fetched = await PrHeadFetch.For(Request("https://github.com/acme/api/pull/7"), Token);

        Assert.Equal(_prSha, fetched.Value!.Head);
    }

    [Fact]
    public async Task A_PR_that_origin_lacks_fails_the_Step_with_gits_words_and_no_branch()
    {
        var fetched = await PrHeadFetch.For(Request("8"), Token);

        Assert.True(fetched.IsError);
        Assert.Equal(SindriErrors.SkillPrFetchFailed.Code, fetched.Error.Code);
        Assert.Contains("refs/pull/8/head", fetched.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_input_that_is_not_a_PR_fails_before_any_git_runs()
    {
        var fetched = await PrHeadFetch.For(Request("not-a-pr"), Token);

        Assert.Equal(SindriErrors.SkillPrInputInvalid.Code, fetched.Error.Code);
    }

    [Fact]
    public async Task A_launch_with_no_skill_or_no_pr_input_fetches_nothing()
    {
        Assert.Null((await PrHeadFetch.For(Request(pr: null), Token)).Value);
        var other = Request("7") with { Skill = new SkillLaunch("officeHours", SkillTarget.ClaudeCode, SkillFixtures.OfficeHours, new Dictionary<string, string>(StringComparer.Ordinal) { ["idea"] = "x" }) };
        Assert.Null((await PrHeadFetch.For(other, Token)).Value);
    }
}
