using Aw.Meta.Core;
using Aw.Meta.Features.Team;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Team;

public sealed class TeamTests
{
    private readonly InMemoryMetaConfig _config = new();

    private readonly InMemoryWorktreeFiles _files = new();

    private TeamInitService Init() => new(_config, _files, new MetaPaths("/state/meta", "/state/meta/gates", Heavy: null, "gh", "/home/u"));

    private TeamShowService Show() => new(_config);

    private static TeamInitRequest Request(string team = "t1", string[]? lanes = null, bool force = false) =>
        new(team, "/work/repo", lanes ?? ["api", "ui"], "meta", Log: null, force);

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_writes_a_starter_config_with_the_meta_lane_and_a_repo_scope()
    {
        var made = await Init().Handle(Request(), CancellationToken.None);
        var loaded = await _config.Load(CancellationToken.None);

        Assert.True(made.IsSuccess);
        Assert.Equal(["meta", "api", "ui"], loaded.Value.Lanes);
        Assert.Equal(("t1", "/work/repo", "meta"), (loaded.Value.Team, loaded.Value.Repo, loaded.Value.MetaLane));
        Assert.Equal(["repo"], loaded.Value.Gate.Always);
        Assert.Equal(["**"], loaded.Value.Gate.Scopes["repo"].Paths);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_writes_the_default_worktree_root_and_the_claude_directory()
    {
        _ = await Init().Handle(Request(), CancellationToken.None);

        Assert.Equal(["/home/u/repos/aw-wt/repo", "/work/repo/.claude/worktrees"], (await _config.Load(CancellationToken.None)).Value.Worktrees.Roots);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_writes_the_configured_worktree_root()
    {
        _files.ConfigureRoot("/work/repo", "/data/trees/repo");

        _ = await Init().Handle(Request(), CancellationToken.None);

        Assert.Equal(["/data/trees/repo", "/work/repo/.claude/worktrees"], (await _config.Load(CancellationToken.None)).Value.Worktrees.Roots);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_is_refused_and_writes_nothing_when_the_root_is_inside_the_repo()
    {
        _files.ConfigureRoot("/work/repo", "trees");

        var refused = await Init().Handle(Request(), CancellationToken.None);

        Assert.Equal(MetaErrors.WorktreeRootInRepo.Code, refused.Error.Code);
        Assert.Equal(MetaErrors.NoTeamConfig.Code, (await _config.Load(CancellationToken.None)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_keeps_a_meta_lane_the_caller_already_listed()
    {
        _ = await Init().Handle(Request(lanes: ["api", "meta"]), CancellationToken.None);

        Assert.Equal(["api", "meta"], (await _config.Load(CancellationToken.None)).Value.Lanes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_refuses_to_replace_a_config_unless_forced()
    {
        _ = await Init().Handle(Request(), CancellationToken.None);

        var refused = await Init().Handle(Request(team: "other"), CancellationToken.None);
        var forced = await Init().Handle(Request(team: "other", force: true), CancellationToken.None);

        Assert.Equal(MetaErrors.ConfigExists.Code, refused.Error.Code);
        Assert.Equal("other", forced.Value.Team);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "api")]
    [InlineData("t1", "")]
    [InlineData("t1", "api,api")]
    [InlineData("t1", "a b")]
    public void TeamInit_input_that_cannot_name_sessions_is_refused(string team, string lanes)
    {
        var lane = lanes.Length == 0 ? [] : lanes.Split(',');

        var result = new TeamInitValidator().Validate(Request(team, lane));

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamShow_returns_the_config_or_AW_META_002()
    {
        var none = await Show().Handle(new TeamShowRequest(), CancellationToken.None);
        _ = await Init().Handle(Request(), CancellationToken.None);
        var some = await Show().Handle(new TeamShowRequest(), CancellationToken.None);

        Assert.Equal(MetaErrors.NoTeamConfig.Code, none.Error.Code);
        Assert.Equal("t1", some.Value.Team);
    }
}
