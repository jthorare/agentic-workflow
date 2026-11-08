using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Launches;

/// <summary>What <see cref="LaunchOptions.For"/> reads: projections only.</summary>
public sealed record LaunchOptionInputs
{
    /// <summary>The item (its engine part says whether an instance has it taken over).</summary>
    public required ItemRow Item { get; init; }

    /// <summary>The item's launches, newest first.</summary>
    public required IReadOnlyList<LaunchRow> Launches { get; init; }

    /// <summary>The last probe with budgets, or null when none was recorded.</summary>
    public LaunchTargets? Probe { get; init; }

    /// <summary>The approved profile's launch settings, or null.</summary>
    public LaunchSettings? Settings { get; init; }

    /// <summary>The newest started launch in the item's repo, or null (any item).</summary>
    public AgentId? LastUsedInRepo { get; init; }

    /// <summary>The item's repo path from the approved profile, or null.</summary>
    public string? RepoPath { get; init; }

    /// <summary>Whether an agent resolves in the catalog.</summary>
    public required Func<AgentId, bool> InCatalog { get; init; }

    /// <summary>Now.</summary>
    public required DateTimeOffset Now { get; init; }
}
