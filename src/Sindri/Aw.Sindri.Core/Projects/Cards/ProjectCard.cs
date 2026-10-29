using System.Text.Json.Serialization;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>One card on the Projects page (projects page spec §3, §6.2): a summary, never a task list.</summary>
/// <param name="Key">What the card answers to (written as "id"): a 'prj_…' id for attached and hand-made projects, the Linear key for a led one.</param>
/// <param name="Origin">Led, attached or hand-made.</param>
/// <param name="Name">Linear's name, or the owner's for a hand-made project.</param>
/// <param name="Description">Linear's description, or null.</param>
/// <param name="State">The state: the owner's when set, else Linear's mapped to the six words; null when neither is known.</param>
/// <param name="StateSetByOwner">Whether the owner's state overrides Linear's.</param>
/// <param name="Health">Linear's health, or null.</param>
/// <param name="Lead">The lead, or null (hand-made, or Linear has none).</param>
/// <param name="Target">The target date: Linear's, or the owner's for a hand-made project.</param>
/// <param name="OpenIssues">Stored open issues; null for a hand-made project or an attached one no longer synced.</param>
/// <param name="Counting">The issue count isn't final yet ("counting…").</param>
/// <param name="OpenPrs">Ship 2: open PRs; null until then.</param>
/// <param name="PrScope">Ship 2: "(updated in the last N days)"; null until then.</param>
/// <param name="LastActivity">The newest change: the project's or its issues', or the owner's last edit for a hand-made project.</param>
/// <param name="FinishedAt">When it became Done or Canceled (Linear's closedAt, or the owner's event), else null.</param>
/// <param name="Repos">Ship 2: repos of linked PRs; empty until then.</param>
/// <param name="Links">The Linear link first, then the owner's links.</param>
/// <param name="Note">The owner's one line, or null.</param>
/// <param name="Synced">False for an attached project the store no longer holds ("no longer synced from Linear").</param>
/// <param name="Version">The version a write sends: the attached row's, or the hide row's for a led project (0 when never hidden).</param>
public sealed record ProjectCard(
    [property: JsonPropertyName("id")] string Key,
    ProjectOrigin Origin,
    string Name,
    string? Description,
    ProjectState? State,
    bool StateSetByOwner,
    ProjectHealth? Health,
    ProjectLead? Lead,
    DateOnly? Target,
    int? OpenIssues,
    bool Counting,
    int? OpenPrs,
    string? PrScope,
    DateTimeOffset? LastActivity,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Repos,
    IReadOnlyList<ProjectLink> Links,
    string? Note,
    bool Synced,
    long Version);
