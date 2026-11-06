namespace Aw.Context.Core.Linear;

/// <summary>What a Linear issue brings besides its record: its fields and its newest comments (at most <see cref="MaxComments"/>, newest first).</summary>
/// <param name="Fields">The fields.</param>
/// <param name="Comments">The comments.</param>
public sealed record LinearIssueSide(LinearIssueFields Fields, IReadOnlyList<LinearCommentRow> Comments)
{
    /// <summary>How many comments an issue keeps.</summary>
    public const int MaxComments = 50;
}
