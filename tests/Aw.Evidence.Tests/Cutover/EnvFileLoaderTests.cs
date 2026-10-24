using Aw.Evidence.Infrastructure.Settings;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// Cutover gap G-d (spec §3.3), loader half: run and parity read the deployment's env files as doctor.sh and the
/// TypeScript runner did (SKILL 'Configuration'): plain NAME=value lines parsed, never sourced; an allowlist of keys; a variable already
/// set in the environment is never overridden. Precedence: the environment, then the local file (.ui-evidence.local.env or what
/// UI_EVIDENCE_CONFIG names), then deployment.env. The files sit in UI_EVIDENCE_ENV_DIR, else .agentic-workflow/ui-evidence under the working directory.
/// </summary>
public sealed class EnvFileLoaderTests
{
    private const string Local = "/skill/.ui-evidence.local.env";
    private const string Deployment = "/skill/deployment.env";

    private static Func<string, string?> Layered(IReadOnlyDictionary<string, string> environment, IReadOnlyDictionary<string, string> files) =>
        EnvFileLoader.Overlay(name => environment.GetValueOrDefault(name), [Local, Deployment], path => files.GetValueOrDefault(path));

    private static Dictionary<string, string> Set(params (string Name, string Value)[] pairs) =>
        pairs.ToDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Name_value_lines_comments_an_export_prefix_and_one_layer_of_quotes_are_read()
    {
        var read = Layered(Set(), Set((Local, "# the app\n\nUI_EVIDENCE_APP_TITLE=Acme\nexport UI_EVIDENCE_API_PORT=\"5002\"\nUI_EVIDENCE_USER_AGENT='Mozilla 5.0'\n  UI_EVIDENCE_SEED_TABLE=public.users\n")));

        Assert.Equal(["Acme", "5002", "Mozilla 5.0", "public.users"], [read("UI_EVIDENCE_APP_TITLE"), read("UI_EVIDENCE_API_PORT"), read("UI_EVIDENCE_USER_AGENT"), read("UI_EVIDENCE_SEED_TABLE")]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_value_is_text_never_a_command_or_an_expansion()
    {
        var read = Layered(Set(), Set((Local, "UI_EVIDENCE_APP_TITLE=$(touch /tmp/pwned)\nUI_EVIDENCE_SEED_TABLE=$HOME`id`;echo pwned\n")));

        Assert.Equal("$(touch /tmp/pwned)", read("UI_EVIDENCE_APP_TITLE"));
        Assert.Equal("$HOME`id`;echo pwned", read("UI_EVIDENCE_SEED_TABLE"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_variable_set_in_the_environment_is_never_overridden_by_a_file_and_a_blank_one_counts_as_unset()
    {
        var read = Layered(
            Set(("UI_EVIDENCE_APP_TITLE", "From env"), ("UI_EVIDENCE_API_PORT", "  ")),
            Set((Local, "UI_EVIDENCE_APP_TITLE=From file\nUI_EVIDENCE_API_PORT=5002\n")));

        Assert.Equal("From env", read("UI_EVIDENCE_APP_TITLE"));
        Assert.Equal("5002", read("UI_EVIDENCE_API_PORT"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_local_file_wins_over_the_committed_deployment_file_name_by_name()
    {
        var read = Layered(
            Set(),
            Set(
                (Local, "UI_EVIDENCE_APP_TITLE=Local title\n"),
                (Deployment, "UI_EVIDENCE_APP_TITLE=Deployment title\nUI_EVIDENCE_PREVIEW_HOST_PATTERN=pr-[0-9]+\\.preview\\.example\\.com\n")));

        Assert.Equal("Local title", read("UI_EVIDENCE_APP_TITLE"));
        Assert.Equal("pr-[0-9]+\\.preview\\.example\\.com", read("UI_EVIDENCE_PREVIEW_HOST_PATTERN"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("HOME")]
    [InlineData("PATH")]
    [InlineData("LD_PRELOAD")]
    [InlineData("AW_STATE_DIR")]          // a file can't move the state directory (and with it the locks)
    [InlineData("AW_QA_STACK_LOCK")]
    [InlineData("AW_HEAVY_LOCK_HELD")]
    public void A_name_outside_the_allowlist_is_ignored_in_a_file(string name)
    {
        var read = Layered(Set(), Set((Local, $"{name}=/tmp/evil\nUI_EVIDENCE_APP_TITLE=Acme\n")));

        Assert.Null(read(name));
        Assert.Equal("Acme", read("UI_EVIDENCE_APP_TITLE"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_outside_the_allowlist_still_comes_from_the_environment()
    {
        var read = Layered(Set(("AW_STATE_DIR", "/state")), Set((Local, "AW_STATE_DIR=/tmp/evil\n")));

        Assert.Equal("/state", read("AW_STATE_DIR"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_or_unreadable_file_is_skipped_and_a_malformed_line_does_not_stop_the_rest()
    {
        var read = Layered(Set(), Set((Deployment, "this is not a setting\n=novalue\n1BAD=x\nUI_EVIDENCE_APP_TITLE=Acme\n")));

        Assert.Equal("Acme", read("UI_EVIDENCE_APP_TITLE"));
        Assert.Null(read("UI_EVIDENCE_API_PORT"));
    }
}
