namespace Aw.Sindri.Core.Profile;

/// <summary>
/// Which providers a Judge Step asks (DT-J plan Q8, owner 2026-10-17): the repo's list, else the box's, else <see cref="Default"/>. 'jev' is
/// opt-in: only a profile list that names it runs it. A recipe's own list only narrows. 'rules' is always kept, last.
/// </summary>
public static class JudgeProviders
{
    /// <summary>The deterministic fallback provider, never dropped.</summary>
    public const string Rules = "rules";

    /// <summary>The opt-in provider.</summary>
    public const string Jev = "jev";

    /// <summary>Every provider name a profile may list.</summary>
    public static IReadOnlyList<string> Known { get; } = [Jev, "claude-cli", "codex-cli", "cursor-cli", Rules];

    /// <summary>The list when the profile sets none.</summary>
    public static IReadOnlyList<string> Default { get; } = ["claude-cli", "codex-cli", "cursor-cli", Rules];

    /// <summary>The providers to ask.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="repo">The item's repo name, or null.</param>
    /// <param name="recipeLiteral">The recipe's own list, or null; it narrows and never adds.</param>
    /// <returns>The list, in order, ending in 'rules'.</returns>
    public static IReadOnlyList<string> Effective(SindriProfile profile, string? repo, IReadOnlyList<string>? recipeLiteral)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var configured = profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo, StringComparison.Ordinal))?.Workflow?.Judge?.Providers
            ?? profile.Workflow?.Judge?.Providers
            ?? Default;
        var narrowed = recipeLiteral is null ? configured : [.. configured.Where(p => recipeLiteral.Contains(p, StringComparer.Ordinal))];
        return [.. narrowed.Where(static p => !string.Equals(p, Rules, StringComparison.Ordinal)), Rules];
    }
}
