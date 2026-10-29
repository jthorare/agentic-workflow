using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// What a child inherits when a question asks for the minimal environment (<see cref="QuestionRef.MinimalEnvironment"/>): the shared
/// <see cref="ClaudeCliOptions.InheritedVariables"/> list plus the variables its CLI's own login needs, by exact name. No GH_TOKEN,
/// AW_FORGE_TOKEN, cloud key or other token of the caller's passes. A box that logs in through an API-key variable not named here
/// needs it added (the login lists are the owner's to check on a logged-in box).
/// </summary>
internal static class AgentCliEnvironment
{
    private static readonly string[] _claude = [.. ClaudeCliOptions.InheritedVariables];

    // CODEX_HOME holds the ChatGPT login (auth.json); without it codex reads ~/.codex.
    private static readonly string[] _codex = [.. ClaudeCliOptions.InheritedVariables, "CODEX_HOME"];

    // CURSOR_API_KEY is how cursor-agent logs in headless; its browser login lives under HOME.
    private static readonly string[] _cursor = [.. ClaudeCliOptions.InheritedVariables, "CURSOR_API_KEY"];

    /// <summary>The variables the CLI's child inherits under the minimal environment.</summary>
    /// <param name="cli">The agent CLI.</param>
    /// <returns>Exact variable names.</returns>
    public static IReadOnlyCollection<string> Minimal(AgentCliName cli) => cli switch
    {
        AgentCliName.ClaudeCli => _claude,
        AgentCliName.CodexCli => _codex,
        AgentCliName.CursorCli => _cursor,
        _ => throw new ArgumentOutOfRangeException(nameof(cli), cli, "unknown agent CLI"),
    };
}
