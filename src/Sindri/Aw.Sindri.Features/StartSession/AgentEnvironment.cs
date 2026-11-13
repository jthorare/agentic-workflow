using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.StartSession;

/// <summary>
/// What a one-off session's environment holds (agents spec §15.4): HOME, PATH, USER, LANG, LC_*, SHELL, TERM, the agent's home variable and its sign-in variable when set, and AW_FLEET_DIR and AW_STATE_DIR
/// so the fleet hook writes where Sindri reads. Sindri's tokens, every other AW_ variable and GH_TOKEN never pass. AW_SINDRI_ONEOFF=1 is set, so the sindri CLI
/// inside never calls as the person, though it runs as the same Unix user and could read the human token file (#3466).
/// </summary>
internal static class AgentEnvironment
{
    private static readonly string[] _common =
    [
        "HOME", "PATH", "USER", "LANG", "SHELL", "TERM",
        "LC_ALL", "LC_CTYPE", "LC_COLLATE", "LC_MESSAGES", "LC_MONETARY", "LC_NUMERIC", "LC_TIME",
        "AW_FLEET_DIR", "AW_STATE_DIR",
    ];

    internal static IReadOnlyDictionary<string, string> Of(IEnvironmentReader environment, AgentId agent)
    {
        var names = agent.Value switch
        {
            "claude-code" => [.. _common, "CLAUDE_CONFIG_DIR", "ANTHROPIC_API_KEY"],
            "codex" => [.. _common, "CODEX_HOME", "OPENAI_API_KEY"],
            _ => _common,
        };
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (environment.Get(name) is { } value)
            {
                env[name] = value;
            }
        }

        env["AW_SINDRI_ONEOFF"] = "1";
        return env;
    }

    /// <summary>The argv: the agent's executable, then the prompt after '--' as one element, so a prompt that starts with '-' arrives as typed. No prompt starts the agent at its own prompt.</summary>
    internal static IReadOnlyList<string> Argv(AgentEntry agent, string? prompt) =>
        string.IsNullOrEmpty(prompt) ? [agent.Bin] : [agent.Bin, "--", prompt];
}
