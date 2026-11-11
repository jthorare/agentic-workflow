namespace Aw.Meta.Core.Github;

/// <summary>Reads the JSON `gh issue list` prints. Anything unexpected is "nothing", never an exception.</summary>
public static class GhIssues
{
    /// <summary>Reads `gh issue list --json number,title,state,closedAt,url`.</summary>
    /// <param name="json">The output.</param>
    /// <returns>The issues, or null when the output is not a JSON array.</returns>
    public static IReadOnlyList<IssueRef>? List(string json) => throw new NotSupportedException();
}
