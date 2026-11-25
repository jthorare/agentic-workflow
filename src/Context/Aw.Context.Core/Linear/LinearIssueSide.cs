namespace Aw.Context.Core.Linear;

/// <summary>What a Linear issue brings besides its record: its fields and its newest comments (at most <see cref="MaxComments"/>, newest first).</summary>
/// <param name="Fields">The fields.</param>
/// <param name="Comments">The comments.</param>
/// <param name="StateName">The state's name as Linear sent it with the issue, or null; a state change logs it beside the ids so a change reads without the team's state list.</param>
/// <param name="Description">The description as Linear has it (raw before the driver cleans it, at most <see cref="Sync.RecordBatches.MaxIssueDescription"/> characters after); null when the issue has none.</param>
public sealed record LinearIssueSide(LinearIssueFields Fields, IReadOnlyList<LinearCommentRow> Comments, string? Description = null, string? StateName = null)
{
    /// <summary>How many comments an issue keeps.</summary>
    public const int MaxComments = 50;
}
