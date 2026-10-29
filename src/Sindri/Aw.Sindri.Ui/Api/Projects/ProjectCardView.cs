namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>One card of the Projects page (projects page spec §6.2): a summary, never a task list.</summary>
/// <param name="Id">A "prj_…" id for an attached or hand-made project, the "linear:project/&lt;uuid&gt;" key for a led one.</param>
/// <param name="Origin">"led", "attached" or "handMade".</param>
/// <param name="Name">Linear's name, or the owner's for a hand-made project.</param>
/// <param name="Description">Linear's description; always null for now.</param>
/// <param name="State">The state word; null when neither the owner nor Linear set one.</param>
/// <param name="StateSetByOwner">Whether the owner's state overrides Linear's.</param>
/// <param name="Health">"onTrack", "atRisk" or "offTrack"; null when Linear has none.</param>
/// <param name="Lead">The lead, or null.</param>
/// <param name="Target">The target date ("2026-01-31"), or null.</param>
/// <param name="OpenIssues">Open issues; null for a hand-made project or one no longer synced.</param>
/// <param name="Counting">The issue count isn't final yet ("counting…").</param>
/// <param name="OpenPrs">Open PRs; null until the PR count ships.</param>
/// <param name="PrScope">The PR count's scope words ("(updated in the last N days)"); null until it ships.</param>
/// <param name="LastActivity">The newest change; null when unknown.</param>
/// <param name="FinishedAt">When it became Done or Canceled; null while open.</param>
/// <param name="Repos">The repos of linked PRs; empty until the PR count ships.</param>
/// <param name="Links">The links.</param>
/// <param name="Note">The owner's one line, or null. Becomes Notes when the API's notes field lands.</param>
/// <param name="Synced">False for an attached project the store no longer holds.</param>
/// <param name="Version">The version a write sends.</param>
internal sealed record ProjectCardView(
    string Id,
    string Origin,
    string Name,
    string? Description,
    string? State,
    bool StateSetByOwner,
    string? Health,
    ProjectLeadView? Lead,
    string? Target,
    int? OpenIssues,
    bool Counting,
    int? OpenPrs,
    string? PrScope,
    DateTimeOffset? LastActivity,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Repos,
    IReadOnlyList<ProjectLinkView> Links,
    string? Note, // becomes Notes when the API's notes field lands
    bool Synced,
    long Version);
