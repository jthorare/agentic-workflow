namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// The model `claude -p` runs with. The defaults are the chain's provider: haiku, low effort, thinking off (which is why
/// MAX_THINKING_TOKENS is 0 and alwaysThinkingEnabled false: ~2.7 s instead of ~15 s). The adjudicator asks opus with high
/// effort and thinking on (<see cref="Adjudicator"/>).
/// </summary>
/// <param name="Model">The --model value.</param>
/// <param name="Effort">The --effort value.</param>
/// <param name="Thinking">Whether the model may think: false sets MAX_THINKING_TOKENS=0 and alwaysThinkingEnabled false; true sets neither limit and alwaysThinkingEnabled true.</param>
/// <param name="MinimalEnvironment">Whether the child inherits only <see cref="InheritedVariables"/> (the adjudicator's does), so no token of the caller's reaches it.</param>
internal sealed record ClaudeCliOptions(string Model = "haiku", string Effort = "low", bool Thinking = false, bool MinimalEnvironment = false)
{
    /// <summary>What the chain's claude-cli provider runs with.</summary>
    internal static ClaudeCliOptions Default { get; } = new();

    /// <summary>What 'judge adjudicate' runs with: opus, high effort, thinking on, a minimal environment.</summary>
    internal static ClaudeCliOptions Adjudicator { get; } = new("opus", "high", Thinking: true, MinimalEnvironment: true);

    /// <summary>
    /// The only variables a <see cref="MinimalEnvironment"/> child inherits: where to find the binary and the user's
    /// own `claude` login (HOME, XDG and CLAUDE_CONFIG_DIR hold it), the locale and the temp directory. No GH_TOKEN,
    /// AW_FORGE_TOKEN, ANTHROPIC_API_KEY or any other token passes.
    /// </summary>
    internal static IReadOnlyList<string> InheritedVariables { get; } =
    [
        "PATH", "HOME", "USER", "LOGNAME", "SHELL", "LANG", "LC_ALL", "LC_CTYPE", "TERM", "TMPDIR", "TZ",
        "XDG_CONFIG_HOME", "XDG_DATA_HOME", "XDG_CACHE_HOME", "XDG_STATE_HOME", "XDG_RUNTIME_DIR", "CLAUDE_CONFIG_DIR",
    ];
}
