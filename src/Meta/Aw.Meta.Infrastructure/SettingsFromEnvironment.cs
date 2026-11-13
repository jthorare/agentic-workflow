using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static MetaSettings Read(Func<string, string?> environment)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = environment("AW_STATE_DIR") is { Length: > 0 } configured ? configured : Path.Combine(home, ".agentic-workflow");
        var metaRoot = Path.Combine(root, "meta");
        var selected = environment("AW_META_TEAM");
        var (state, layout) = TeamResolver.Resolve(metaRoot, selected);
        var heavy = environment("AW_HEAVY") is { Length: > 0 } wrapper ? wrapper : Path.Combine(root, "bin-r1b", "heavy");
        return new MetaSettings(
            state,
            OverrideConfig(environment("AW_META_CONFIG"), selected) ?? Path.Combine(state, "team.json"),
            environment("AW_CLAUDE_SESSIONS_DIR") is { Length: > 0 } sessions ? sessions : Path.Combine(home, ".claude", "sessions"),
            environment("AW_HEAVY_JOB_LOCK") is { Length: > 0 } heavyLock ? heavyLock : Path.Combine(root, "locks", "heavy-job.lock"),
            Path.Combine(state, "lane-sessions"),
            environment("AW_TMUX") is { Length: > 0 } tmux ? tmux : "tmux",
            environment("AW_META_TMUX_SOCKET") is { Length: > 0 } socket ? socket : null,
            new MetaPaths(state, Path.Combine(state, "gates"), File.Exists(heavy) ? heavy : null, environment("AW_GH") is { Length: > 0 } gh ? gh : "gh", home, UserConfig(environment, home)),
            layout);
    }

    // Where the installer writes the systemd drop-in: $XDG_CONFIG_HOME, else <home>/.config.
    private static string UserConfig(Func<string, string?> environment, string home) =>
        environment("XDG_CONFIG_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".config");

    // AW_META_CONFIG overrides the config path, but a lane's own variable must not carry over to another team: when a team is selected
    // and the file names a different one (a lane of team A running `aw-meta --team B ...`), the override is ignored.
    private static string? OverrideConfig(string? path, string? selected) =>
        path is { Length: > 0 } && (string.IsNullOrEmpty(selected) || TeamResolver.LegacyTeam(path) == selected) ? path : null;
}
