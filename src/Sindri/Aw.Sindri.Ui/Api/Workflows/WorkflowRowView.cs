namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One row of GET /sindri/workflows (#2258, PR 8a).</summary>
/// <param name="Slug">The name commands use.</param>
/// <param name="Name">The name for people.</param>
/// <param name="ActiveRevision">The revision triggers run, or null when the workflow is off.</param>
/// <param name="LatestRevision">The newest revision.</param>
/// <param name="ChangedBy">Who saved the newest revision: "owner" or "session:&lt;id&gt;".</param>
/// <param name="ChangedAt">When the newest revision was saved.</param>
/// <param name="Archived">True for an archived workflow; only a list asked with includeArchived carries it (#2702).</param>
/// <param name="Trigger">The trigger in words ("By hand", "Every weekday 7:00"), or null (#3683, not shipped yet).</param>
/// <param name="Paused">True when the workflow has a schedule but is held; null from a Sindri that does not say (#3683).</param>
/// <param name="NextRunAt">When a schedule runs next, or null (#3683).</param>
/// <param name="LastRun">The last run, or null when it never ran (#3683).</param>
/// <param name="Recent">The last 10 runs, newest first, or null (#3683).</param>
internal sealed record WorkflowRowView(
    string Slug,
    string Name,
    int? ActiveRevision,
    int LatestRevision,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    bool? Archived = null,
    string? Trigger = null,
    bool? Paused = null,
    DateTimeOffset? NextRunAt = null,
    WorkflowLastRunView? LastRun = null,
    IReadOnlyList<WorkflowRecentRunView>? Recent = null);
