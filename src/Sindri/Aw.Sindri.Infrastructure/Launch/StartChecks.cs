using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The checks before a one-off start (agents spec §15.4): the agent's executable is on PATH, and the agent has a credential (its sign-in variable, or its own login file); the repo's folder exists.
/// Claude Code keeps its login in '~/.claude/.credentials.json' (Linux) or the keychain with an 'oauthAccount' in '~/.claude.json', or in '$CLAUDE_CONFIG_DIR' under the same two names when that is set (#3471);
/// Codex keeps it in '$CODEX_HOME/auth.json'. An agent Sindri has no rule for counts as signed in.
/// </summary>
/// <param name="environment">Reads one environment variable.</param>
internal sealed class StartChecks(Func<string, string?> environment) : IStartChecks
{
    public ValueTask<AgentReadiness> Agent(AgentEntry agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var home = environment("HOME") ?? string.Empty;
        var (signedIn, command) = agent.Id.Value switch
        {
            "claude-code" => (Has("ANTHROPIC_API_KEY") || ClaudeLogin(home), "claude"),
            "codex" => (Has("OPENAI_API_KEY") || File.Exists(Path.Combine(environment("CODEX_HOME") is { Length: > 0 } codex ? codex : Path.Combine(home, ".codex"), "auth.json")), "codex login"),
            _ => (true, agent.Bin),
        };
        return ValueTask.FromResult(new AgentReadiness(OnPath(agent.Bin), signedIn, command));
    }

    public bool FolderExists(string path) => Directory.Exists(path);

    private bool Has(string name) => environment(name) is { Length: > 0 };

    /// <summary>A Claude Code login in $CLAUDE_CONFIG_DIR first (when set), then in the default home.</summary>
    private bool ClaudeLogin(string home) =>
        (environment("CLAUDE_CONFIG_DIR") is { Length: > 0 } config && (File.Exists(Path.Combine(config, ".credentials.json")) || ClaudeAccount(Path.Combine(config, ".claude.json"))))
        || File.Exists(Path.Combine(home, ".claude", ".credentials.json"))
        || ClaudeAccount(Path.Combine(home, ".claude.json"));

    private bool OnPath(string bin)
    {
        if (bin.Contains('/', StringComparison.Ordinal))
        {
            return File.Exists(bin);
        }

        return (environment("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Any(dir => File.Exists(Path.Combine(dir, bin)));
    }

    private static bool ClaudeAccount(string file)
    {
        try
        {
            return File.Exists(file) && File.ReadAllText(file).Contains("\"oauthAccount\"", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
