using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>What only the owner-copy source does: the copy, its mode, its binds, the expiry margin and the cleanup (spike item 2).</summary>
[Lane(TestLane.Integration)]
public sealed class OwnerCopyCredentialSourceTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('e', 32));
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private static UnixFileMode Mode(string path) => OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(path);

    [Fact]
    public async Task The_copy_is_mode_0600_in_a_0700_directory_and_leaves_no_temp_file()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        var source = home.Source();

        var prepared = (await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        var directory = prepared.Handle.Value;
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(directory + "/.credentials.json"));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(directory + "/.claude.json"));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, Mode(directory));
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        Assert.Equal(await File.ReadAllTextAsync(home.Root + "/.claude/.credentials.json", TestContext.Current.CancellationToken), await File.ReadAllTextAsync(directory + "/.credentials.json", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_agent_home_is_read_only_the_copy_is_the_config_directory_and_the_login_file_is_never_bound()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);

        var prepared = (await home.Source().Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        var config = home.Root + "/.claude-run";
        Assert.Equal(new LoginMount(home.Root + "/.claude", home.Root + "/.claude", MountMode.ReadOnly), prepared.Mounts[0]);
        Assert.Equal(new LoginMount(prepared.Handle.Value, config, MountMode.ReadWrite), prepared.Mounts[1]);
        Assert.Contains(new LoginMount(home.Root + "/.claude/settings.json", config + "/settings.json", MountMode.ReadOnly), prepared.Mounts);
        Assert.Contains(new LoginMount(home.Root + "/.claude/hooks", config + "/hooks", MountMode.ReadOnly), prepared.Mounts);
        Assert.DoesNotContain(prepared.Mounts, m => m.SandboxPath.Contains("/projects", StringComparison.Ordinal) || m.SandboxPath.EndsWith("credentials.json", StringComparison.Ordinal));
        Assert.Equal(config, prepared.Environment["CLAUDE_CONFIG_DIR"]);
        Assert.Equal([config], prepared.DeniedReads);
    }

    [Fact]
    public async Task Codex_gets_its_home_as_a_copy_with_the_sessions_directory_shared()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);

        var prepared = (await home.Source().Prepare(AgentId.From("codex"), _launch, TestContext.Current.CancellationToken)).Value;

        var config = home.Root + "/.codex-run";
        Assert.Equal(config, prepared.Environment["CODEX_HOME"]);
        Assert.Contains(new LoginMount(home.Root + "/.codex/sessions", config + "/sessions", MountMode.ReadWrite), prepared.Mounts);
        Assert.Contains(new LoginMount(home.Root + "/.codex/config.toml", config + "/config.toml", MountMode.ReadOnly), prepared.Mounts);
        Assert.True(File.Exists(prepared.Handle.Value + "/auth.json"));
    }

    [Fact]
    public void A_valid_login_reports_when_it_runs_out_and_an_expired_one_is_expired()
    {
        using var valid = new FakeOwnerHome(LoginState.Ready, TimeSpan.FromHours(3));
        using var expired = new FakeOwnerHome(LoginState.Expired);

        var ready = valid.Source().Probe(_claude);
        var gone = expired.Source().Probe(_claude);

        Assert.Equal((LoginState.Ready, FakeOwnerHome.Now + TimeSpan.FromHours(3)), (ready.State, ready.ExpiresAt));
        Assert.Equal(LoginState.Expired, gone.State);
        Assert.Contains("open Claude Code once", gone.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Codex_login_older_than_its_refresh_age_is_017_expired()
    {
        using var home = new FakeOwnerHome(LoginState.Ready, codexRefreshedDaysAgo: 17);

        var status = home.Source().Probe(AgentId.From("codex"));

        Assert.Equal(LoginState.Expired, status.State);
        Assert.Contains("Codex", status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_recent_codex_login_is_ready_until_the_refresh_age_after_last_refresh()
    {
        using var home = new FakeOwnerHome(LoginState.Ready, codexRefreshedDaysAgo: 2);

        var status = home.Source().Probe(AgentId.From("codex"));

        Assert.Equal((LoginState.Ready, FakeOwnerHome.Now - TimeSpan.FromDays(2) + TimeSpan.FromDays(8)), (status.State, status.ExpiresAt));
    }

    private static string CodexJwtAuth(DateTimeOffset exp, string lastRefresh)
    {
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{{\"exp\":{exp.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)}}}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{{\"tokens\":{{\"access_token\":\"h.{payload}.s\"}},\"last_refresh\":\"{lastRefresh}\"}}";
    }

    [Fact]
    public void Codex_login_refreshed_12_days_ago_is_not_ready_even_when_the_token_has_hours_left()
    {
        using var home = new FakeOwnerHome(LoginState.Ready, codexRefreshedDaysAgo: 12);
        home.CodexAuth(CodexJwtAuth(FakeOwnerHome.Now + TimeSpan.FromHours(2), "2026-10-06T12:00:00Z"));

        var status = home.Source().Probe(AgentId.From("codex"));

        Assert.Equal(LoginState.Expired, status.State);
    }

    [Fact]
    public void Codex_expiry_is_the_earlier_of_the_tokens_exp_and_last_refresh_plus_the_refresh_age()
    {
        using var home = new FakeOwnerHome(LoginState.Ready, codexRefreshedDaysAgo: 1);
        var exp = FakeOwnerHome.Now + TimeSpan.FromHours(2);
        home.CodexAuth(CodexJwtAuth(exp, "2026-10-17T12:00:00Z"));

        var status = home.Source().Probe(AgentId.From("codex"));

        Assert.Equal((LoginState.Ready, DateTimeOffset.FromUnixTimeSeconds(exp.ToUnixTimeSeconds())), (status.State, status.ExpiresAt));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public void A_login_file_that_cannot_be_parsed_is_no_login_not_a_login_without_an_expiry(string text)
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        File.WriteAllText(home.Root + "/.claude/.credentials.json", text);

        var status = home.Source().Probe(_claude);

        Assert.Equal(LoginState.Missing, status.State);
    }

    [Fact]
    public async Task The_copied_claude_json_keeps_the_names_of_the_owners_mcp_servers_and_none_of_their_secrets()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);

        var prepared = (await home.Source().Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        var copy = await File.ReadAllTextAsync(prepared.Handle.Value + "/.claude.json", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("ghp_", copy, StringComparison.Ordinal);
        Assert.DoesNotContain("ownerbearer", copy, StringComparison.Ordinal);
        Assert.DoesNotContain("proj-secret", copy, StringComparison.Ordinal);
        Assert.Contains("GITHUB_TOKEN", copy, StringComparison.Ordinal);
        Assert.Contains("Authorization", copy, StringComparison.Ordinal);
        Assert.Contains("owner@example.com", copy, StringComparison.Ordinal);
        Assert.Contains("https://x.example", copy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_claude_json_that_cannot_be_parsed_is_not_copied_at_all()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        await File.WriteAllTextAsync(home.Root + "/.claude.json", "{ broken", TestContext.Current.CancellationToken);

        var prepared = (await home.Source().Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        Assert.False(File.Exists(prepared.Handle.Value + "/.claude.json"));
        Assert.True(File.Exists(prepared.Handle.Value + "/.credentials.json"));
    }

    [Fact]
    public async Task The_files_the_cli_writes_in_the_agent_home_are_not_bound_read_only()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);

        var prepared = (await home.Source().Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        Assert.DoesNotContain(prepared.Mounts, m => m.SandboxPath.EndsWith("/.last-cleanup", StringComparison.Ordinal) || m.SandboxPath.EndsWith("/mcp-needs-auth-cache.json", StringComparison.Ordinal));
    }

    [Fact]
    public void The_scratch_names_are_the_spikes_write_set_without_the_two_login_files()
    {
        string[] spike = ["projects", "sessions", "session-env", "shell-snapshots", "file-history", "todos", "history.jsonl", "backups", "cache", "state", "mcp-needs-auth-cache.json", ".last-update-result.json", ".last-cleanup"];

        Assert.Equal(spike.Order(StringComparer.Ordinal), OwnerCopyCredentialSource.ClaudeScratch.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Release_removes_the_copy_and_nothing_else_and_a_foreign_handle_is_ignored()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        var source = home.Source();
        var prepared = (await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        source.Release(new LoginHandle(home.Root + "/.claude"));
        Assert.True(File.Exists(home.Root + "/.claude/settings.json"));
        source.Release(prepared.Handle);

        Assert.False(Directory.Exists(prepared.Handle.Value));
        Assert.False(Directory.Exists(home.SindriHome + "/launches/" + _launch.Value));
        Assert.True(File.Exists(home.Root + "/.claude/.credentials.json"));
    }

    [Fact]
    public async Task The_copy_is_stamped_before_the_first_file_is_copied_so_a_sweep_never_sees_it_unstamped()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        var seen = new List<bool>();
        var clock = new ObservingClock(home.Clock, () =>
        {
            // Stamp writes the mark and then reads the clock: at that moment no login file may be in the copy directory yet.
            foreach (var mark in Directory.Exists(home.SindriHome + "/launches") ? Directory.GetFiles(home.SindriHome + "/launches", ".prepared", SearchOption.AllDirectories) : [])
            {
                seen.Add(File.Exists(Path.GetDirectoryName(mark) + "/.credentials.json"));
            }
        });
        var source = new OwnerCopyCredentialSource(new OwnerCopyOptions(home.Root, home.SindriHome), clock);

        _ = await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.NotEmpty(seen);
        Assert.All(seen, copied => Assert.False(copied, "a login file was already copied when the stamp was made"));
    }

    private sealed class ObservingClock(TimeProvider inner, Action onRead) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            onRead();
            return inner.GetUtcNow();
        }
    }

    [Fact]
    public async Task The_crash_sweep_removes_the_copies_of_launches_with_no_live_scope_once_they_are_past_the_grace_period()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        var source = home.Source();
        var live = LaunchId.From("LCH-" + new string('f', 32));
        var dead = (await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;
        var kept = (await source.Prepare(_claude, live, TestContext.Current.CancellationToken)).Value;

        var tooSoon = source.SweepStale(static _ => false);   // still preparing: no scope yet, but not dead
        home.Clock.Advance(TimeSpan.FromMinutes(3));
        var removed = source.SweepStale(id => string.Equals(id, live.Value, StringComparison.Ordinal));

        Assert.Equal(0, tooSoon);
        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(dead.Handle.Value));
        Assert.True(Directory.Exists(kept.Handle.Value));
    }
}
