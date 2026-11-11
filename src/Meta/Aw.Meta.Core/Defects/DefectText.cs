using Aw.Meta.Core.Github;

namespace Aw.Meta.Core.Defects;

/// <summary>The text of a defect issue and its comments, built only from the command's own fields.</summary>
public static class DefectText
{
    /// <summary>How long a closed issue still counts as the same defect.</summary>
    public static TimeSpan ClosedWindow { get; } = TimeSpan.FromDays(14);

    /// <summary>The issue title, which is also the dedup key.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="title">The short title.</param>
    /// <returns>"code: title".</returns>
    public static string Title(string code, string title) => throw new NotSupportedException();

    /// <summary>The body of the issue or comment.</summary>
    /// <param name="what">What happened.</param>
    /// <param name="errorLine">The exact error line.</param>
    /// <param name="path">The code or doc path; null or blank leaves the "Where" line out.</param>
    /// <returns>The markdown.</returns>
    public static string Body(string what, string errorLine, string? path) => throw new NotSupportedException();

    /// <summary>The issue this defect was already reported as.</summary>
    /// <param name="issues">The issues the search returned.</param>
    /// <param name="title">The issue title.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The lowest-numbered open issue, or one closed within <see cref="ClosedWindow"/>, with the same title; null for none.</returns>
    public static IssueRef? Match(IReadOnlyList<IssueRef> issues, string title, DateTimeOffset now) => throw new NotSupportedException();
}
