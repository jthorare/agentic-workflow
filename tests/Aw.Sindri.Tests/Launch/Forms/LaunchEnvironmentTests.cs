using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>The scrub a launched agent Step's environment gets (LN-LCH1): no forge credential, an empty gh config, the worktree named.</summary>
public sealed class LaunchEnvironmentTests
{
    private static readonly string[] _credentials = ["GH_TOKEN", "GITHUB_TOKEN", "GH_ENTERPRISE_TOKEN", "AW_FORGE_TOKEN", "AW_REVIEW_GITHUB_TOKEN"];

    [Fact, Trait("Lane", "unit")]
    public void Every_forge_credential_is_blanked_even_when_the_launch_set_one()
    {
        var env = LaunchEnvironment.Scrub(new Dictionary<string, string>(StringComparer.Ordinal) { ["GH_TOKEN"] = "ghp_secret" }, "/work/wt", "/gh");

        foreach (var name in _credentials)
        {
            Assert.Equal(string.Empty, env[name]);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_gh_config_directory_and_the_worktree_are_set()
    {
        var env = LaunchEnvironment.Scrub(new Dictionary<string, string>(StringComparer.Ordinal), "/work/wt", "/gh");

        Assert.Equal("/gh", env["GH_CONFIG_DIR"]);
        Assert.Equal("/work/wt", env["AW_SINDRI_WORKTREE"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Other_variables_stay_and_the_input_is_not_changed()
    {
        var input = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_ITEM"] = "ITM-1", ["GITHUB_TOKEN"] = "x" };

        var env = LaunchEnvironment.Scrub(input, "/work/wt", "/gh");

        Assert.Equal("ITM-1", env["AW_SINDRI_ITEM"]);
        Assert.Equal("x", input["GITHUB_TOKEN"]);
        Assert.Equal(2, input.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_review_posting_token_is_blanked_by_default_and_a_custom_env_name_is_blanked_too()
    {
        var input = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_REVIEW_GITHUB_TOKEN"] = "ghp_review", ["MY_REVIEW_TOKEN"] = "ghp_custom" };

        var env = LaunchEnvironment.Scrub(input, "/work/wt", "/gh", [PrReviewRules.CredentialVariable(new PrReviewSettings(Credential: "env:MY_REVIEW_TOKEN"))!]);

        Assert.Equal(string.Empty, env["AW_REVIEW_GITHUB_TOKEN"]);
        Assert.Equal(string.Empty, env["MY_REVIEW_TOKEN"]);
        Assert.Equal("ghp_custom", input["MY_REVIEW_TOKEN"]);
    }
}
