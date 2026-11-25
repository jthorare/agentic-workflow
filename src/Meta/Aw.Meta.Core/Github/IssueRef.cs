namespace Aw.Meta.Core.Github;

/// <summary>An issue as `gh issue list --json number,title,state,closedAt,url` reports it.</summary>
/// <param name="Number">The number.</param>
/// <param name="Title">The title.</param>
/// <param name="IsOpen">Whether it is open.</param>
/// <param name="ClosedAt">When it was closed; null while open or when GitHub gave none.</param>
/// <param name="Url">The web address.</param>
public sealed record IssueRef(int Number, string Title, bool IsOpen, DateTimeOffset? ClosedAt, string Url);
