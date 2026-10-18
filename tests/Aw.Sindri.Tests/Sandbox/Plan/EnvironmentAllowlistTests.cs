using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The environment allowlist and the secret-pattern check (sandbox spec §3.4).</summary>
public sealed class EnvironmentAllowlistTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("GH_TOKEN")]
    [InlineData("GITHUB_PAT")]
    [InlineData("AWS_SECRET_ACCESS_KEY")]
    [InlineData("AGENT_APP_KEY_FILE")]
    [InlineData("MY_PASSWORD")]
    [InlineData("STRIPE_KEY")]
    [InlineData("SOME_SECRET")]
    [InlineData("DB_PASSWORD_FILE")]
    public void Secret_shaped_names_are_recognized(string name) => Assert.True(EnvironmentAllowlist.IsSecretShaped(name));

    [Theory, Trait("Lane", "unit")]
    [InlineData("HOME")]
    [InlineData("PATH")]
    [InlineData("GIT_CONFIG_KEY_0")]
    [InlineData("AW_SINDRI_LAUNCH")]
    public void Ordinary_names_are_not(string name) => Assert.False(EnvironmentAllowlist.IsSecretShaped(name));

    [Fact, Trait("Lane", "unit")]
    public void Only_the_listed_secret_shaped_names_pass()
    {
        string[] names = ["GH_TOKEN", "PATH", "AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY", "OPENAI_API_KEY", "AGENT_APP_ID"];

        Assert.Equal(["GH_TOKEN", "AGENT_APP_ID"], EnvironmentAllowlist.Violations(names));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_listed_name_has_a_reason_and_the_lock_held_variable_is_not_listed()
    {
        Assert.NotEmpty(EnvironmentAllowlist.Names);
        Assert.All(EnvironmentAllowlist.Names, n => Assert.False(string.IsNullOrWhiteSpace(n.Value), n.Key));
        Assert.DoesNotContain("AW_HEAVY_LOCK_HELD", EnvironmentAllowlist.Names.Keys, StringComparer.Ordinal);
        foreach (var name in new[] { "PATH", "HOME", "LANG", "TERM", "TMPDIR", "USER", "SHELL", "DOTNET_ROOT", "DOTNET_CLI_HOME", "NUGET_PACKAGES", "NVM_DIR", "npm_config_ignore_scripts", "GIT_CONFIG_GLOBAL", "AW_SANDBOX", "AW_SANDBOX_PLAN", "AW_SINDRI_LAUNCH_SECRET", "HTTPS_PROXY", "NO_PROXY", "DOTNET_PROCESSOR_COUNT", "MSBUILDDISABLENODEREUSE", "DOTNET_CLI_USE_MSBUILD_SERVER" })
        {
            Assert.Contains(name, EnvironmentAllowlist.Names.Keys, StringComparer.Ordinal);
        }
    }
}
