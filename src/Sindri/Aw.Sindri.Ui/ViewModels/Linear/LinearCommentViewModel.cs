namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One comment on the issue sheet.</summary>
/// <param name="Author">Who wrote it.</param>
/// <param name="When">How long ago, in words ("30m ago").</param>
/// <param name="Lines">The body, a line each.</param>
/// <param name="FromSindri">True when Sindri wrote it.</param>
internal sealed record LinearCommentViewModel(string Author, string When, IReadOnlyList<LinearTextLine> Lines, bool FromSindri)
{
    /// <summary>The comment's heading as read aloud: "Sam Lee, 30m ago".</summary>
    public string Heading => Author + ", " + When;
}
