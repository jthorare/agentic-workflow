using Aw.Context.Core.Records;

namespace Aw.Context.Core.Linear;

/// <summary>An issue record's side rows (spec 2026-11-04 §4.1): the fields and newest comments the <see cref="WriterId"/> writer stores as <c language="text">linear_issue</c> and <c language="text">linear_comment</c> rows, and the assignee and state changes it logs as <c language="text">linear_issue_change</c> rows.</summary>
/// <param name="Issue">The issue's fields and comments.</param>
/// <param name="Written">The fields a write-through sent; a change in one of them is logged as 'write-through', every other change as 'feed'. A tick's page sends none.</param>
public sealed record LinearSideRows(LinearIssueSide Issue, LinearWrittenFields Written = LinearWrittenFields.None) : SideRows(WriterId)
{
    /// <summary>The writer's id.</summary>
    public const string WriterId = "linear";
}
