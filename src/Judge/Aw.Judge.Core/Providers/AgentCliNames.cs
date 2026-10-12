namespace Aw.Judge.Core.Providers;

/// <summary>The agent CLIs' provider names, binaries and default order.</summary>
public static class AgentCliNames
{
    /// <summary>The default preference when AW_PROVIDER names none: claude, codex, cursor (fastest measured first).</summary>
    public static IReadOnlyList<AgentCliName> DefaultOrder { get; } = [AgentCliName.ClaudeCli, AgentCliName.CodexCli, AgentCliName.CursorCli];

    /// <summary>The provider this agent CLI is.</summary>
    /// <param name="value">The agent CLI.</param>
    /// <returns>The provider.</returns>
    public static ProviderName Provider(AgentCliName value) => value switch
    {
        AgentCliName.ClaudeCli => ProviderName.ClaudeCli,
        AgentCliName.CodexCli => ProviderName.CodexCli,
        AgentCliName.CursorCli => ProviderName.CursorCli,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "unknown agent CLI"),
    };

    /// <summary>The host's headless binary, looked up on PATH.</summary>
    /// <param name="value">The agent CLI.</param>
    /// <returns>'claude', 'codex' or 'cursor-agent'.</returns>
    public static string Binary(AgentCliName value) => value switch
    {
        AgentCliName.ClaudeCli => "claude",
        AgentCliName.CodexCli => "codex",
        AgentCliName.CursorCli => "cursor-agent",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "unknown agent CLI"),
    };

    /// <summary>The agent CLI a provider is, if it is one.</summary>
    /// <param name="value">The provider.</param>
    /// <returns>The agent CLI, or null for rules and jev.</returns>
    public static AgentCliName? FromProvider(ProviderName value) => value switch
    {
        ProviderName.ClaudeCli => AgentCliName.ClaudeCli,
        ProviderName.CodexCli => AgentCliName.CodexCli,
        ProviderName.CursorCli => AgentCliName.CursorCli,
        ProviderName.Rules or ProviderName.Jev => null,
        _ => null,
    };

    /// <summary>
    /// Reads AW_PROVIDER, the host the calling session runs in: 'claude', 'codex' or 'cursor', or the '-cli' provider
    /// name, trimmed and in any case.
    /// </summary>
    /// <param name="value">The variable's value, or null when unset.</param>
    /// <returns>The agent CLI, or null when unset or unknown.</returns>
    public static AgentCliName? FromHost(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        var candidate = trimmed.EndsWith("-cli", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "-cli";
        foreach (var name in DefaultOrder)
        {
            if (string.Equals(ProviderNames.Name(Provider(name)), candidate, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }
}
