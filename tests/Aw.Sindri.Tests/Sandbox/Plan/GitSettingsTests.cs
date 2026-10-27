using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The sandbox's git settings, delivered at command-line scope (sandbox spec §3.4).</summary>
public sealed class GitSettingsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_settings_are_a_fixed_identity_no_signing_no_credential_helper_and_a_per_launch_hooks_path()
    {
        var settings = GitSettings.For("/work/web/wt", "/run/githooks").ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);

        Assert.Equal("Sindri Step", settings["user.name"]);
        Assert.Equal("sindri-step@localhost", settings["user.email"]);
        Assert.Equal("false", settings["commit.gpgsign"]);
        Assert.Equal("false", settings["tag.gpgsign"]);
        Assert.Equal(string.Empty, settings["credential.helper"]);
        Assert.Equal("/work/web/wt", settings["safe.directory"]);
        Assert.Equal("0", settings["gc.auto"]);
        Assert.Equal("never", settings["gc.pruneExpire"]);
        Assert.Equal("/run/githooks", settings["core.hooksPath"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Git_settings_beat_the_repo_config_by_travelling_as_command_line_scope_environment()
    {
        var settings = GitSettings.For("/wt", "/hooks");

        var env = GitSettings.ToEnvironment(settings, "/run/gitconfig.empty").ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);

        Assert.Equal(settings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), env["GIT_CONFIG_COUNT"]);
        Assert.Equal(settings[0].Key, env["GIT_CONFIG_KEY_0"]);
        Assert.Equal(settings[0].Value, env["GIT_CONFIG_VALUE_0"]);
        Assert.Equal(settings[^1].Key, env["GIT_CONFIG_KEY_" + (settings.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        Assert.Equal("/run/gitconfig.empty", env["GIT_CONFIG_GLOBAL"]);
    }
}
