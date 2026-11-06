namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One PR on the Reviews list (review page spec §6.1).</summary>
/// <param name="Repo">owner/name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="Title">The PR's title.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Draft">True for a draft PR.</param>
/// <param name="OpenedAt">When it opened: the row's age.</param>
/// <param name="LastActivity">Its newest activity: the order inside a section.</param>
/// <param name="Waiting">"requested" or "newCommits" in the Waiting on you group; null in Yours.</param>
/// <param name="WaitingText">"review requested", "reviewed, 2 new commits" or "reviewed, new commits"; null in Yours.</param>
/// <param name="NewCommits">Commits since the owner's latest review; null when unknown or not waiting on one.</param>
/// <param name="Ci">"pass", "fail", "running" or "none".</param>
/// <param name="RequiredOpen">Open required checks; null when the project has none decided.</param>
/// <param name="RequiredText">"2 open", "none open"; null with <paramref name="RequiredOpen"/>.</param>
/// <param name="ClaimsDoc">True when the PR names a design doc (a Covers: line).</param>
/// <param name="Href">The page model's path ("/reviews/acme/api/2071").</param>
internal sealed record ReviewRowView(
    string Repo,
    int Number,
    string Title,
    string Author,
    bool Draft,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastActivity,
    string? Waiting,
    string? WaitingText,
    int? NewCommits,
    string Ci,
    int? RequiredOpen,
    string? RequiredText,
    bool ClaimsDoc,
    string Href);
