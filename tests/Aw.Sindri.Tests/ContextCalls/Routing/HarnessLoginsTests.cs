using Aw.Context.Core.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The 'context:models' row's login check (model routing spec §4 "Environment"): a harness whose only login is a variable the minimal environment drops.</summary>
[Lane(TestLane.Unit)]
public sealed class HarnessLoginsTests
{
    private static HarnessLogins Logins(Dictionary<string, string> environment, string[]? files = null, bool keychain = false) =>
        new(environment.GetValueOrDefault, path => (files ?? []).Contains(path, StringComparer.Ordinal), keychain);

    private static Dictionary<string, string> Env(params (string Name, string Value)[] variables) => variables.ToDictionary(static v => v.Name, static v => v.Value, StringComparer.Ordinal);

    [Fact]
    public void Claude_signed_in_only_through_ANTHROPIC_API_KEY_is_named_with_its_fix()
    {
        var (detail, fix) = Assert.Single(Logins(Env(("HOME", "/home/a"), ("ANTHROPIC_API_KEY", "sk-ant-secret"))).Problems([ModelHarness.ClaudeCode]));

        Expect.Equal("claude-code signs in through ANTHROPIC_API_KEY, which context model calls don't pass (they run with a minimal environment), and it has no stored login", detail);
        Expect.Equal("run claude and sign in with /login, so its login is stored under ~/.claude", fix);
        Assert.DoesNotContain("sk-ant-secret", detail + fix, StringComparison.Ordinal);
    }

    [Fact]
    public void Codex_signed_in_only_through_OPENAI_API_KEY_is_named_and_CODEX_HOME_is_where_its_login_is_looked_for()
    {
        var environment = Env(("HOME", "/home/a"), ("CODEX_HOME", "/srv/codex"), ("OPENAI_API_KEY", "sk-secret"));

        var (detail, fix) = Assert.Single(Logins(environment, files: ["/home/a/.codex/auth.json"]).Problems([ModelHarness.Codex]));

        Expect.Equal("codex signs in through OPENAI_API_KEY, which context model calls don't pass (they run with a minimal environment), and it has no stored login", detail);
        Expect.Equal("run codex login (for a key: printenv OPENAI_API_KEY | codex login --with-api-key), so its login is stored in auth.json", fix);
        Assert.Empty(Logins(environment, files: ["/srv/codex/auth.json"]).Problems([ModelHarness.Codex]));
    }

    [Fact]
    public void A_stored_login_beside_the_variable_is_fine_and_CLAUDE_CONFIG_DIR_is_where_Claude_s_is_looked_for()
    {
        var environment = Env(("HOME", "/home/a"), ("CLAUDE_CONFIG_DIR", "/srv/claude"), ("CLAUDE_CODE_OAUTH_TOKEN", "t"));

        Assert.Empty(Logins(environment, files: ["/srv/claude/.credentials.json"]).Problems([ModelHarness.ClaudeCode]));
        _ = Assert.Single(Logins(environment, files: ["/home/a/.claude/.credentials.json"]).Problems([ModelHarness.ClaudeCode]));
    }

    [Fact]
    public void No_login_variable_set_names_nothing() =>
        Assert.Empty(Logins(Env(("HOME", "/home/a"))).Problems([ModelHarness.ClaudeCode, ModelHarness.Codex, ModelHarness.Cursor]));

    [Fact]
    public void Claude_on_a_keychain_box_and_cursor_are_never_named() =>
        Assert.Empty(Logins(Env(("HOME", "/home/a"), ("ANTHROPIC_API_KEY", "k"), ("CURSOR_API_KEY", "k")), keychain: true).Problems([ModelHarness.ClaudeCode, ModelHarness.Cursor]));

    [Fact]
    public void A_harness_no_route_runs_is_not_checked() =>
        Assert.Empty(Logins(Env(("HOME", "/home/a"), ("OPENAI_API_KEY", "k"))).Problems([ModelHarness.ClaudeCode]));
}
