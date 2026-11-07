namespace Aw.Sindri.Core.Reviews;

/// <summary>The PR header (spec §6.2).</summary>
/// <param name="Repo">owner/name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="Title">The title (forge text).</param>
/// <param name="Author">The author's login (forge text).</param>
/// <param name="Yours">Whether the author is review.login (case-insensitive).</param>
/// <param name="Draft">Whether it is a draft.</param>
/// <param name="State">open, closed or merged.</param>
/// <param name="Head">The head commit and branch.</param>
/// <param name="Base">The base branch.</param>
/// <param name="Url">The PR on the forge.</param>
/// <param name="Issues">The Linear issues the PR's record links to.</param>
/// <param name="PostsFixThreads">True when the PR's author is review.login or the forge credential's own login: the PR where a fix thread can be posted as the owner or as Sindri.</param>
public sealed record ReviewPagePr(
    string Repo,
    int Number,
    string Title,
    string Author,
    bool Yours,
    bool Draft,
    string State,
    ReviewPageHead Head,
    string Base,
    string Url,
    IReadOnlyList<ReviewPageIssue> Issues,
    bool PostsFixThreads);
