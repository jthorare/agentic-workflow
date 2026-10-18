using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class NamedTeamsCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private string Meta(params string[] parts) => Path.Combine([_cli.File("meta"), .. parts]);

    private async Task Init(string team, string lanes = "api") =>
        Assert.Equal(0, (await _cli.Run("team-init", "--team", team, "--repo", "/r-" + team, "--lanes", lanes)).Exit);

    [Fact, Trait("Lane", "integration")]
    public async Task Team_init_writes_only_that_teams_config_and_leaves_the_others_alone()
    {
        await Init("alpha");
        var before = await File.ReadAllTextAsync(Meta("teams", "alpha", "team.json"), TestContext.Current.CancellationToken);

        await Init("beta", "ui");
        var again = await _cli.Run("team-init", "--team", "alpha", "--repo", "/other", "--lanes", "x");

        Assert.Equal(before, await File.ReadAllTextAsync(Meta("teams", "alpha", "team.json"), TestContext.Current.CancellationToken));
        Assert.Contains("\"/r-beta\"", await File.ReadAllTextAsync(Meta("teams", "beta", "team.json"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.False(File.Exists(Meta("team.json")));
        Assert.Equal((2, "AW-META-011"), (again.Exit, again.Err[..11]));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task With_one_team_none_needs_selecting_and_with_several_a_command_exits_2_listing_them()
    {
        await Init("alpha");
        var single = await _cli.Run("team-show");
        await Init("beta");

        var ambiguous = await _cli.Run("team-show");
        var picked = await _cli.Run("--team", "beta", "team-show");
        _cli.SetEnv("AW_META_TEAM", "alpha");
        var fromEnv = await _cli.Run("team-show");
        var flagWins = await _cli.Run("--team", "beta", "team-show");

        Assert.Contains("alpha:", single.Out, StringComparison.Ordinal);
        Assert.Equal((2, "AW-META-019"), (ambiguous.Exit, ambiguous.Err[..11]));
        Assert.Contains("alpha, beta", ambiguous.Err, StringComparison.Ordinal);
        Assert.Contains("--team", ambiguous.Err, StringComparison.Ordinal);
        Assert.Contains("beta:", picked.Out, StringComparison.Ordinal);
        Assert.Contains("alpha:", fromEnv.Out, StringComparison.Ordinal);
        Assert.Contains("beta:", flagWins.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Two_teams_share_no_role_obligations_owner_queue_or_log()
    {
        await Init("alpha");
        await Init("beta");
        var transcript = _cli.File("meta.jsonl");
        await File.WriteAllTextAsync(transcript, MetaWorld.PeerLine("m1", "alpha-api", "OWNER QUESTION: sqlite or postgres?"), TestContext.Current.CancellationToken);

        var setup = new[]
        {
            await _cli.Run("--team", "alpha", "role-set", "--session", "s-alpha", "--name", "alpha-meta"),
            await _cli.Run("--team", "alpha", "inbox-sync", "--session", "s-alpha", "--transcript", transcript),
            await _cli.Run("--team", "alpha", "log-append", "--kind", "note", "--text", "alpha only"),
        };
        Assert.All(setup, r => Assert.Equal(0, r.Exit));

        var alphaInbox = await _cli.Run("--team", "alpha", "inbox");
        var betaInbox = await _cli.Run("--team", "beta", "inbox");
        var alphaOwner = await _cli.Run("--team", "alpha", "owner");
        var betaOwner = await _cli.Run("--team", "beta", "owner");
        var betaRole = await _cli.Run("--team", "beta", "role-show");
        var alphaRole = await _cli.Run("--team", "alpha", "role-show");
        var betaStop = await _cli.Run("--team", "beta", "stop-check", "--session", "s-alpha", "--transcript", transcript);
        var alphaStop = await _cli.Run("--team", "alpha", "stop-check", "--session", "s-alpha", "--transcript", transcript);

        Assert.Contains("m1", alphaInbox.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("m1", betaInbox.Out, StringComparison.Ordinal);
        Assert.Contains("sqlite or postgres?", alphaOwner.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("sqlite", betaOwner.Out, StringComparison.Ordinal);
        Assert.Equal(1, betaRole.Exit);
        Assert.Equal(0, alphaRole.Exit);
        Assert.Equal(0, betaStop.Exit);
        Assert.Equal(1, alphaStop.Exit);
        Assert.True(File.Exists(Meta("teams", "alpha", "meta.db")));
        Assert.Contains("alpha only", await File.ReadAllTextAsync(Meta("teams", "alpha", "log.md"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.False(File.Exists(Meta("teams", "beta", "log.md")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_hook_command_passes_straight_through_when_the_team_cannot_be_told()
    {
        await Init("alpha");
        await Init("beta");

        var stop = await _cli.Run("stop-check", "--session", "s-1", "--transcript", _cli.File("none.jsonl"));
        var outbox = await _cli.RunWithInput("{}", "outbox-record");

        _cli.SetEnv("AW_META_TEAM", "../x");
        var badName = await _cli.Run("stop-check", "--session", "s-1", "--transcript", _cli.File("none.jsonl"));
        var badOther = await _cli.Run("inbox");

        Assert.Equal((0, string.Empty), (stop.Exit, stop.Err));
        Assert.Equal((0, string.Empty), (outbox.Exit, outbox.Err));
        Assert.Equal((0, string.Empty), (badName.Exit, badName.Err));
        Assert.Equal(2, badOther.Exit);
        Assert.False(File.Exists(Meta("meta.db")), "a hook that cannot tell the team must not create the flat state");
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("../x")]
    [InlineData("Alpha")]
    [InlineData("-a")]
    public async Task A_team_name_that_could_be_a_path_trick_is_refused(string name)
    {
        var result = await _cli.Run("--team", name, "team-show");

        Assert.Equal((2, "AW-META-001"), (result.Exit, result.Err[..11]));
        Assert.False(Directory.Exists(Meta("teams")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_legacy_flat_config_keeps_working_with_no_team_or_its_own_team_selected()
    {
        Directory.CreateDirectory(Meta());
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r","lanes":["meta","api"]}""", TestContext.Current.CancellationToken);

        var bare = await _cli.Run("team-show");
        _ = await _cli.Run("role-set", "--session", "s-t2", "--name", "t2-meta");
        var named = await _cli.Run("--team", "t2", "role-show");
        _cli.SetEnv("AW_META_TEAM", "t2");
        var fromEnv = await _cli.Run("team-show");

        Assert.Contains("t2:", bare.Out, StringComparison.Ordinal);
        Assert.Contains("t2-meta", named.Out, StringComparison.Ordinal);
        Assert.Contains("t2:", fromEnv.Out, StringComparison.Ordinal);
        Assert.True(File.Exists(Meta("meta.db")));
        Assert.False(Directory.Exists(Meta("teams")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_legacy_config_wins_over_named_teams_and_a_second_team_gets_its_own_directory()
    {
        Directory.CreateDirectory(Meta());
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r","lanes":["api"]}""", TestContext.Current.CancellationToken);
        await Init("other");

        var bare = await _cli.Run("team-show");
        var other = await _cli.Run("--team", "other", "team-show");

        Assert.Contains("t2:", bare.Out, StringComparison.Ordinal);
        Assert.Contains("other:", other.Out, StringComparison.Ordinal);
        _ = await _cli.Run("role-set", "--session", "s-1", "--name", "t2-meta");
        _ = await _cli.Run("--team", "other", "role-set", "--session", "s-2", "--name", "other-meta");
        Assert.True(File.Exists(Meta("meta.db")));
        Assert.True(File.Exists(Meta("teams", "other", "meta.db")));
        Assert.False(File.Exists(Meta("teams", "t2", "meta.db")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task AW_META_CONFIG_still_overrides_the_config_path_for_its_own_team_and_is_ignored_for_another()
    {
        await Init("alpha");
        await Init("beta");
        var elsewhere = _cli.File("elsewhere.json");
        await File.WriteAllTextAsync(elsewhere, """{"team":"alpha","repo":"/elsewhere","lanes":["api"]}""", TestContext.Current.CancellationToken);
        _cli.SetEnv("AW_META_CONFIG", elsewhere);

        var own = await _cli.Run("--team", "alpha", "team-show");
        var other = await _cli.Run("--team", "beta", "team-show");
        _ = await _cli.Run("--team", "alpha", "role-set", "--session", "s-1", "--name", "alpha-meta");

        Assert.Contains("repo /elsewhere", own.Out, StringComparison.Ordinal);
        Assert.Contains("beta:", other.Out, StringComparison.Ordinal);
        Assert.Contains("repo /r-beta", other.Out, StringComparison.Ordinal);
        Assert.True(File.Exists(Meta("teams", "alpha", "meta.db")), "the state directory is still the selected team's");
        Assert.False(File.Exists(Meta("meta.db")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_list_names_every_team_with_its_repo_lanes_and_role_and_marks_the_selected_one()
    {
        Directory.CreateDirectory(Meta());
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r-t2","lanes":["meta","api"]}""", TestContext.Current.CancellationToken);
        await Init("alpha", "ui");
        _ = await _cli.Run("--team", "alpha", "role-set", "--session", "s-a", "--name", "alpha-meta");

        var all = await _cli.Run("--team", "alpha", "team-list");
        var json = await _cli.Run("team-list", "--json");

        Assert.Equal(0, all.Exit);
        Assert.Contains("* alpha [named]: repo /r-alpha, lanes meta, ui, meta alpha-meta (not running)", all.Out, StringComparison.Ordinal);
        Assert.Contains("  t2 [legacy]: repo /r-t2, lanes meta, api, no meta role", all.Out, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"t2\"", json.Out.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_migrate_moves_the_legacy_files_into_the_teams_directory_and_leaves_gates()
    {
        Directory.CreateDirectory(Meta("lane-sessions"));
        Directory.CreateDirectory(Meta("gates", "7"));
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r","lanes":["api"]}""", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Meta("team.json.init.bak"), "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Meta("lane-sessions", "api"), "abc", TestContext.Current.CancellationToken);
        _ = await _cli.Run("role-set", "--session", "s-t2", "--name", "t2-meta");

        var dry = await _cli.Run("team-migrate", "--dry-run");
        var dryMoved = File.Exists(Meta("team.json"));
        var done = await _cli.Run("team-migrate");
        var shown = await _cli.Run("team-show");
        var role = await _cli.Run("role-show");

        Assert.Contains("would move t2", dry.Out, StringComparison.Ordinal);
        Assert.True(dryMoved);
        Assert.Equal(0, done.Exit);
        Assert.False(File.Exists(Meta("team.json")));
        Assert.False(File.Exists(Meta("meta.db")));
        Assert.Empty(Directory.GetFiles(Meta(), "meta.db*"));
        Assert.True(File.Exists(Meta("teams", "t2", "team.json")));
        Assert.True(File.Exists(Meta("teams", "t2", "team.json.init.bak")));
        Assert.Equal("abc", await File.ReadAllTextAsync(Meta("teams", "t2", "lane-sessions", "api"), TestContext.Current.CancellationToken));
        Assert.True(Directory.Exists(Meta("gates", "7")));
        Assert.Contains("t2:", shown.Out, StringComparison.Ordinal);
        Assert.Contains("t2-meta", role.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_migrate_refuses_when_there_is_nothing_to_migrate_or_the_target_exists()
    {
        var none = await _cli.Run("team-migrate");
        Directory.CreateDirectory(Meta("teams", "t2"));
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r","lanes":["api"]}""", TestContext.Current.CancellationToken);
        var exists = await _cli.Run("team-migrate");

        Assert.Equal((2, "AW-META-002"), (none.Exit, none.Err[..11]));
        Assert.Equal((2, "AW-META-020"), (exists.Exit, exists.Err[..11]));
        Assert.True(File.Exists(Meta("team.json")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Lane_sessions_are_per_team_so_a_lane_of_one_team_is_unknown_to_another()
    {
        await Init("alpha");
        await Init("beta");
        _ = Directory.CreateDirectory(Meta("teams", "alpha", "lane-sessions"));
        await File.WriteAllTextAsync(Meta("teams", "alpha", "lane-sessions", "api"), "s-api\n", TestContext.Current.CancellationToken);
        _cli.SetEnv("AW_META_LANE", "api");

        _cli.SetEnv("AW_META_TEAM", "alpha");
        var inAlpha = await _cli.RunWithInput("done\n", "lane-reply", "--stdin");
        _cli.SetEnv("AW_META_TEAM", "beta");
        var inBeta = await _cli.RunWithInput("done\n", "lane-reply", "--stdin");

        Assert.Equal(0, inAlpha.Exit);
        Assert.Equal((2, "AW-META-015"), (inBeta.Exit, inBeta.Err[..11]));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_list_and_team_migrate_need_no_team_when_several_exist_and_list_shows_json_fields()
    {
        await Init("alpha");
        await Init("beta");

        var list = await _cli.Run("team-list");
        var json = await _cli.Run("team-list", "--json");
        var dry = await _cli.Run("team-migrate", "--dry-run");

        Assert.Equal(0, list.Exit);
        Assert.Contains("alpha [named]", list.Out, StringComparison.Ordinal);
        Assert.Contains("beta [named]", list.Out, StringComparison.Ordinal);
        var compact = json.Out.Replace(" ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"layout\":\"named\"", compact, StringComparison.Ordinal);
        Assert.Contains("\"lanes\":[\"meta\",\"api\"]", compact, StringComparison.Ordinal);
        Assert.Contains("\"selected\":false", compact, StringComparison.Ordinal);
        Assert.Equal((2, "AW-META-002"), (dry.Exit, dry.Err[..11]));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_list_shows_a_legacy_team_and_a_named_team_of_the_same_name_as_two_rows()
    {
        Directory.CreateDirectory(Meta());
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r","lanes":["api"]}""", TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Meta("teams", "t2"));
        await File.WriteAllTextAsync(Meta("teams", "t2", "team.json"), """{"team":"t2","repo":"/r2","lanes":["api"]}""", TestContext.Current.CancellationToken);

        var list = await _cli.Run("team-list");

        Assert.Contains("* t2 [legacy]", list.Out, StringComparison.Ordinal);
        Assert.Contains("  t2 [named]", list.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_migrate_refuses_a_legacy_config_whose_team_name_cannot_be_a_directory()
    {
        Directory.CreateDirectory(Meta());
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"Bad Name","repo":"/r","lanes":["api"]}""", TestContext.Current.CancellationToken);

        var refused = await _cli.Run("team-migrate");

        Assert.Equal((2, "AW-META-020"), (refused.Exit, refused.Err[..11]));
        Assert.True(File.Exists(Meta("team.json")));
        Assert.False(Directory.Exists(Meta("teams")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_migrate_refuses_while_a_lane_session_of_the_team_is_running()
    {
        Directory.CreateDirectory(Meta("lane-sessions"));
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r","lanes":["api"]}""", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_cli.SessionsDirectory, "1.json"), "{\"pid\":" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"sessionId\":\"s-api\",\"name\":\"t2-api\",\"status\":\"idle\",\"cwd\":\"/w\"}", TestContext.Current.CancellationToken);

        var refused = await _cli.Run("team-migrate");

        Assert.Equal((2, "AW-META-020"), (refused.Exit, refused.Err[..11]));
        Assert.True(File.Exists(Meta("team.json")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_named_team_directory_without_a_config_does_not_hide_the_legacy_team()
    {
        Directory.CreateDirectory(Meta());
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r","lanes":["api"]}""", TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Meta("teams", "t2"));

        var shown = await _cli.Run("--team", "t2", "team-show");

        Assert.Contains("t2:", shown.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Role_and_health_need_a_selected_team_when_several_exist()
    {
        await Init("alpha");
        await Init("beta");

        var role = await _cli.Run("role-show");
        var health = await _cli.Run("health");

        Assert.Equal((2, "AW-META-019"), (role.Exit, role.Err[..11]));
        Assert.Equal((2, "AW-META-019"), (health.Exit, health.Err[..11]));
        Assert.False(File.Exists(Meta("meta.db")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_init_for_the_legacy_teams_own_name_is_refused_and_creates_no_team_directory()
    {
        Directory.CreateDirectory(Meta());
        await File.WriteAllTextAsync(Meta("team.json"), """{"team":"t2","repo":"/r","lanes":["api"]}""", TestContext.Current.CancellationToken);

        var refused = await _cli.Run("team-init", "--team", "t2", "--repo", "/r", "--lanes", "api");

        Assert.Equal((2, "AW-META-011"), (refused.Exit, refused.Err[..11]));
        Assert.False(Directory.Exists(Meta("teams")));
    }

    public void Dispose() => _cli.Dispose();
}
