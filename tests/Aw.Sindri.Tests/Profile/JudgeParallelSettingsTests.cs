using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'workflow.judge.parallel' (issue 1440): how many judge calls a judge Step runs at once, read, checked, resolved repo-first and shown in the diff.</summary>
public sealed class JudgeParallelSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    [Fact, Trait("Lane", "integration")]
    public async Task Workflow_judge_parallel_reads_at_the_box_and_the_repo()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  judge:\n    parallel: 6\nrepos:\n  - name: app\n    path: /a\n    workflow:\n      judge:\n        parallel: 12\n");

        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        Assert.Equal(6, loaded.Value.Profile.Workflow?.Judge?.Parallel);
        Assert.Equal(12, loaded.Value.Profile.Repos?[0].Workflow?.Judge?.Parallel);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_value_wins_over_the_box_value_and_unset_is_null()
    {
        var profile = _valid with
        {
            Workflow = new(Judge: new(Parallel: 6)),
            Repos = [new RepoEntry("app", "/a", Workflow: new(Judge: new(Parallel: 12))), new RepoEntry("lib", "/b")],
        };

        Assert.Equal(12, JudgeParallel.Effective(profile, "app"));
        Assert.Equal(6, JudgeParallel.Effective(profile, "lib"));
        Assert.Equal(6, JudgeParallel.Effective(profile, repo: null));
        Assert.Null(JudgeParallel.Effective(_valid, "app"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(16)]
    public void The_ends_of_the_range_are_accepted(int parallel) =>
        Assert.False(ProfileRules.Check(_valid with { Workflow = new(Judge: new(Parallel: parallel)) }).IsError);

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(-3)]
    public void A_box_value_outside_1_to_16_is_a_profile_error_at_its_path(int parallel)
    {
        var error = ProfileRules.Check(_valid with { Workflow = new(Judge: new(Parallel: parallel)) }).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith("workflow.judge.parallel: ", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(17)]
    public void A_repo_value_outside_1_to_16_names_the_repo(int parallel)
    {
        var profile = _valid with { Repos = [new RepoEntry("app", "/a", Workflow: new(Judge: new(Parallel: parallel)))] };

        var error = ProfileRules.Check(profile).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith("repos.app.workflow.judge.parallel: ", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_the_box_and_the_repo_parallel_keys()
    {
        var live = ProfileRules.Check(_valid).Value;
        var edited = live with
        {
            Workflow = new(Judge: new(Parallel: 6)),
            Repos = [new RepoEntry("app", "/a", Workflow: new(Judge: new(Parallel: 12)))],
        };

        Assert.Equal(["repos.app", "repos.app.workflow.judge.parallel", "workflow.judge.parallel"], [.. ProfileDiff.Between(live, edited).Order(StringComparer.Ordinal)]);
    }
}
