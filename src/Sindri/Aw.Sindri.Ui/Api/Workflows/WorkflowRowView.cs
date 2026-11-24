namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One row of GET /sindri/workflows (#2258, PR 8a).</summary>
/// <param name="Slug">The name commands use.</param>
/// <param name="Name">The name for people.</param>
/// <param name="ActiveRevision">The revision triggers run, or null when the workflow is off.</param>
/// <param name="LatestRevision">The newest revision.</param>
/// <param name="ChangedBy">Who saved the newest revision: "owner" or "session:&lt;id&gt;".</param>
/// <param name="ChangedAt">When the newest revision was saved.</param>
/// <param name="Archived">True for an archived workflow; only a list asked with includeArchived carries it (#2702).</param>
/// <param name="Trigger">The trigger in words ("By hand", "Every weekday 7:00"); empty when the graph has no trigger node (#3683).</param>
/// <param name="Paused">True when the workflow was on once and is off now (#3683); null from an older Sindri.</param>
/// <param name="NextRunAt">When the workflow's schedule fires next, or null (off, no schedule, or not known yet) (#3758).</param>
/// <param name="LastRun">The newest run of any status, or null when it never ran (#3683).</param>
/// <param name="Recent">The newest 10 runs, newest first; empty when it never ran, null from an older Sindri (#3683).</param>
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
