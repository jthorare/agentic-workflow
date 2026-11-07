namespace Aw.Sindri.Core.Reviews;

/// <summary>What the page header needs of the PR: the forge's read, with the stored record where the forge sends nothing.</summary>
/// <param name="Title">The title.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Draft">Whether it is a draft.</param>
/// <param name="State">open, closed or merged.</param>
/// <param name="HeadSha">The head commit.</param>
/// <param name="HeadRef">The head branch.</param>
/// <param name="Base">The base branch.</param>
/// <param name="Url">The PR on the forge.</param>
/// <param name="Body">The PR body, or null when it was not read (a body check then fails).</param>
/// <param name="IssueKeys">The Linear issue keys the stored record links to.</param>
/// <param name="UpdatedAt">The PR's newest activity, which dates its PR-body evidence.</param>
public sealed record ReviewPagePrFacts(string Title, string Author, bool Draft, string State, string HeadSha, string HeadRef, string Base, string Url, string? Body, IReadOnlyList<string> IssueKeys, DateTimeOffset UpdatedAt);
