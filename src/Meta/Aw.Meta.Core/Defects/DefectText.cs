using System.Text;
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
    public static string Title(string code, string title)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(title);
        return $"{code.Trim()}: {title.Trim()}";
    }

    /// <summary>The body of the issue or comment.</summary>
    /// <param name="what">What happened.</param>
    /// <param name="errorLine">The exact error line.</param>
    /// <param name="path">The code or doc path; null or blank leaves the "Where" line out.</param>
    /// <returns>The markdown.</returns>
    public static string Body(string what, string errorLine, string? path)
    {
        ArgumentNullException.ThrowIfNull(what);
        ArgumentNullException.ThrowIfNull(errorLine);
        var body = new StringBuilder()
            .Append("## What happens\n\n")
            .Append(what.Trim())
            .Append("\n\nError line:\n\n```\n")
            .Append(errorLine.Trim())
            .Append("\n```\n\n");
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = body.Append("Where: `").Append(path.Trim()).Append("`\n\n");
        }

        return body.Append("Filed by `aw-meta report-defect`.").ToString();
    }

    /// <summary>The issue this defect was already reported as.</summary>
    /// <param name="issues">The issues the search returned.</param>
    /// <param name="title">The issue title.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The lowest-numbered open issue, or one closed within <see cref="ClosedWindow"/>, with the same title; null for none.</returns>
    public static IssueRef? Match(IReadOnlyList<IssueRef> issues, string title, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(title);
        return issues
            .Where(i => string.Equals(i.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase)
                && (i.IsOpen || (i.ClosedAt is { } closed && now - closed <= ClosedWindow)))
            .OrderBy(static i => i.Number)
            .FirstOrDefault();
    }
}
