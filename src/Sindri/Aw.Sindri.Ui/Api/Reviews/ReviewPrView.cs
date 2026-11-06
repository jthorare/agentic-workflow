namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The PR's header (§6.2): title, repo, number, author, head and the Linear issues its record links to.</summary>
internal sealed record ReviewPrView(
    string Repo,
    int Number,
    string Title,
    string Author,
    bool Yours,
    bool Draft,
    string State,
    ReviewHeadView Head,
    string Base,
    string Url,
    IReadOnlyList<ReviewIssueView> Issues);
