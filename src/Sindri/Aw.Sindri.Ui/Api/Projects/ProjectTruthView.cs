namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>What Sindri's own PR, item and agent data says about one project (issue 3718): the same fields for a led, attached or hand-made card.</summary>
/// <param name="OpenPrs">The open PRs of the project's repos and PR links.</param>
/// <param name="OpenItems">Items linked to the project that are not done or cancelled.</param>
/// <param name="DoneItems">Items linked to the project that are done.</param>
/// <param name="TotalItems">Every item linked to the project, cancelled ones left out.</param>
/// <param name="RunningAgents">Live agent sessions in the project's repos that are running or asking.</param>
/// <param name="Health">The health Sindri derives from the PRs; null when no PR is open.</param>
internal sealed record ProjectTruthView(
    IReadOnlyList<ProjectTruthPrView> OpenPrs,
    int OpenItems,
    int DoneItems,
    int TotalItems,
    int RunningAgents,
    ProjectTruthHealthView? Health);
