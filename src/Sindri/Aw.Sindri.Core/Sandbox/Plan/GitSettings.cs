using System.Globalization;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The sandbox's git settings, delivered at command-line scope so they beat the repo's own config (sandbox spec §3.4).</summary>
public static class GitSettings
{
    /// <summary>The settings for one launch.</summary>
    /// <param name="worktree">The worktree ('safe.directory'), or null.</param>
    /// <param name="hooksDir">The per-launch directory 'core.hooksPath' points at.</param>
    /// <returns>The settings, in order.</returns>
    public static IReadOnlyList<GitSetting> For(string? worktree, string hooksDir)
    {
        ArgumentNullException.ThrowIfNull(hooksDir);
        List<GitSetting> settings =
        [
            new("user.name", "Sindri Step"),
            new("user.email", "sindri-step@localhost"),
            new("commit.gpgsign", "false"),
            new("tag.gpgsign", "false"),
            new("credential.helper", string.Empty),
        ];
        if (worktree is not null)
        {
            settings.Add(new GitSetting("safe.directory", worktree));
        }

        settings.Add(new GitSetting("gc.auto", "0"));
        settings.Add(new GitSetting("gc.pruneExpire", "never"));
        settings.Add(new GitSetting("core.hooksPath", hooksDir));
        return settings;
    }

    /// <summary>The environment that delivers the settings: GIT_CONFIG_COUNT, GIT_CONFIG_KEY_n, GIT_CONFIG_VALUE_n and GIT_CONFIG_GLOBAL.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="emptyGlobalFile">The path of an empty file for GIT_CONFIG_GLOBAL.</param>
    /// <returns>The variables.</returns>
    public static IReadOnlyList<PlanEnvVar> ToEnvironment(IReadOnlyList<GitSetting> settings, string emptyGlobalFile)
    {
        ArgumentNullException.ThrowIfNull(settings);
        const string Why = "the sandbox's git settings at command-line scope";
        List<PlanEnvVar> env = [new("GIT_CONFIG_COUNT", settings.Count.ToString(CultureInfo.InvariantCulture), Why)];
        for (var i = 0; i < settings.Count; i++)
        {
            var n = i.ToString(CultureInfo.InvariantCulture);
            env.Add(new PlanEnvVar("GIT_CONFIG_KEY_" + n, settings[i].Key, Why));
            env.Add(new PlanEnvVar("GIT_CONFIG_VALUE_" + n, settings[i].Value, Why));
        }

        env.Add(new PlanEnvVar("GIT_CONFIG_GLOBAL", emptyGlobalFile, "an empty file: the owner's .gitconfig is absent"));
        return env;
    }
}
