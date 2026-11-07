namespace Aw.Sindri.Core.Reviews;

/// <summary>A Linear issue chip.</summary>
/// <param name="Key">The issue key (ENG-12).</param>
/// <param name="Href">sindri://linear/KEY.</param>
public sealed record ReviewPageIssue(
    string Key,
    string Href);
