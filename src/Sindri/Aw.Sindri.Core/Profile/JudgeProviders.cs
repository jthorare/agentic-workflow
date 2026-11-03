using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// Which providers a Judge Step asks (DT-J plan Q8): the repo's list, else the box's, else <see cref="Default"/>, which asks Jev first (owner, 2026-11-03;
/// it replaced the 2026-10-17 rule that Jev is opt-in). A recipe's own list only narrows. 'rules' is always kept, last.
/// </summary>
public static class JudgeProviders
{
    /// <summary>The deterministic fallback provider, never dropped.</summary>
    public const string Rules = JudgeProviderNames.Rules;

    /// <summary>The Jev provider.</summary>
    public const string Jev = JudgeProviderNames.Jev;

    /// <summary>Every provider name a profile may list.</summary>
    public static IReadOnlyList<string> Known => JudgeProviderNames.Known;

    /// <summary>The list when the profile sets none.</summary>
    public static IReadOnlyList<string> Default { get; } = [Jev, "claude-cli", "codex-cli", "cursor-cli", Rules];

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
