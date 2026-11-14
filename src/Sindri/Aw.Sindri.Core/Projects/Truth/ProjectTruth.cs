namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>What Sindri's own data says about one project (issue 3718): the same fields for a hand-made, attached or Linear-led card, read by the page, the CLI and MCP.</summary>
/// <param name="OpenPrs">The open PRs of the project's sources.</param>
/// <param name="OpenItems">Items linked to the project that are not done or cancelled.</param>
/// <param name="DoneItems">Items linked to the project that are done.</param>
/// <param name="TotalItems">Every item linked to the project, cancelled ones left out.</param>
/// <param name="RunningAgents">Live agent sessions in the project's repos that are running or asking.</param>
/// <param name="Health">The derived health, or null when there is nothing to judge.</param>
public sealed record ProjectTruth(IReadOnlyList<ProjectTruthPr> OpenPrs, int OpenItems, int DoneItems, int TotalItems, int RunningAgents, ProjectTruthHealth? Health)
{
    /// <summary>No PRs, no items, no agents, no health: what a card carries until the truth is read.</summary>
    public static ProjectTruth Empty { get; } = new([], 0, 0, 0, 0, null);
}
