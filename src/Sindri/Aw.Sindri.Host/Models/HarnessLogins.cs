using Aw.Context.Core.Engine;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The login half of the 'context:models' row (model routing spec §4 "Environment"): context model calls run their CLI with the minimal environment, which drops
/// the API-key variables a CLI can sign in with. A harness on a route whose only login is such a variable (set here, no stored login) can't answer, so it is named
/// with its fix. Cursor isn't checked: CURSOR_API_KEY passes, and its browser login lives under HOME, which passes too.
/// </summary>
/// <param name="environment">Sindri's own environment.</param>
/// <param name="fileExists">Whether a file exists.</param>
/// <param name="keychain">Whether Claude Code keeps its login in the keychain (macOS), where no file shows it.</param>
internal sealed class HarnessLogins(Func<string, string?> environment, Func<string, bool> fileExists, bool keychain)
{
    /// <summary>The logins of this box.</summary>
    /// <param name="environment">Sindri's own environment.</param>
    /// <returns>The check.</returns>
    internal static HarnessLogins Of(Func<string, string?> environment) => new(environment, File.Exists, OperatingSystem.IsMacOS());

    /// <summary>One problem per harness whose only login the minimal environment drops.</summary>
    /// <param name="used">The harnesses the routes run.</param>
    /// <returns>Each problem's detail and fix.</returns>
    internal IEnumerable<(string Detail, string Fix)> Problems(IEnumerable<ModelHarness> used)
    {
        foreach (var harness in used)
        {
            if (Login(harness) is not { } login || login.Variables.FirstOrDefault(v => !string.IsNullOrEmpty(environment(v))) is not { } variable || fileExists(login.Stored))
            {
                continue;
            }

            yield return ($"{ModelHarnesses.Name(harness)} signs in through {variable}, which context model calls don't pass (they run with a minimal environment), and it has no stored login", login.Fix(variable));
        }
    }

    // why: the variables are the CLIs' own API-key logins, none of them in AgentCliEnvironment's minimal lists; a keychain login shows in no file, so Claude isn't judged there
    private (string[] Variables, string Stored, Func<string, string> Fix)? Login(ModelHarness harness) => harness switch
    {
        ModelHarness.ClaudeCode when !keychain => (["ANTHROPIC_API_KEY", "CLAUDE_CODE_OAUTH_TOKEN", "ANTHROPIC_AUTH_TOKEN"],
            Path.Combine(environment("CLAUDE_CONFIG_DIR") ?? Path.Combine(environment("HOME") ?? string.Empty, ".claude"), ".credentials.json"),
            static _ => "run claude and sign in with /login, so its login is stored under ~/.claude"),
        ModelHarness.Codex => (["OPENAI_API_KEY", "CODEX_API_KEY"],
            Path.Combine(environment("CODEX_HOME") ?? Path.Combine(environment("HOME") ?? string.Empty, ".codex"), "auth.json"),
            static variable => $"run codex login (for a key: printenv {variable} | codex login --with-api-key), so its login is stored in auth.json"),
        ModelHarness.ClaudeCode or ModelHarness.Cursor => null,
        _ => null,
    };
}
