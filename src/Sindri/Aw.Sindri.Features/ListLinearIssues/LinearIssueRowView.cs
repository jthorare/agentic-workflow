namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>One issue of the Work page's Linear list (Linear hub spec §3). Title and the names inside are Linear text: fenced for a session.</summary>
/// <param name="Key">The issue key, 'ENG-12'.</param>
/// <param name="Record">The context record key, 'linear:issue/&lt;uuid&gt;'.</param>
/// <param name="Url">The issue on Linear.</param>
/// <param name="Title">The title.</param>
/// <param name="Team">The team.</param>
/// <param name="State">The workflow state.</param>
/// <param name="Priority">0 (none) to 4 (low).</param>
/// <param name="Assignee">The assignee, or null.</param>
/// <param name="Project">The project, or null.</param>
/// <param name="Cycle">The cycle, or null.</param>
/// <param name="Labels">The labels.</param>
/// <param name="UpdatedAt">When Linear last changed the issue.</param>
/// <param name="Link">The Sindri item working it, or null.</param>
/// <param name="Pr">The pull request that item's run opened, or null.</param>
internal sealed record LinearIssueRowView(
    string Key,
    string Record,
    string? Url,
    string Title,
    LinearTeamView Team,
    LinearStateView State,
    int Priority,
    LinearPersonView? Assignee,
    LinearNamedView? Project,
    LinearCycleView? Cycle,
    IReadOnlyList<LinearLabelView> Labels,
    DateTimeOffset UpdatedAt,
    LinearItemLinkView? Link,
    LinearPrView? Pr);
