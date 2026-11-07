
namespace Aw.Sindri.Features.LinearIssues;

/// <summary>One issue as the sheet reads it: the list row's fields at the top level, then the viewer, the description, the newest comments, whether Linear removed it and whether the sheet may edit it.</summary>
/// <param name="Key">The issue key, 'ENG-12'.</param>
/// <param name="Record">The context record key.</param>
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
/// <param name="Pr">That item's pull request, or null.</param>
/// <param name="Viewer">Who "you" is; null until the viewer step has run.</param>
/// <param name="Description">The description (markdown); empty when none.</param>
/// <param name="Comments">The newest 50 comments, newest first.</param>
/// <param name="Removed">True when Linear no longer serves the issue: this is its last stored copy and writes are off.</param>
/// <param name="Write">Whether the sheet may edit it.</param>
internal sealed record GetLinearIssueResponse(
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
    LinearPrView? Pr,
    LinearPersonView? Viewer,
    string Description,
    IReadOnlyList<LinearCommentView> Comments,
    bool Removed,
    LinearWriteGateView Write);
