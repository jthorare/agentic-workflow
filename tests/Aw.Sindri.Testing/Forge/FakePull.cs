using System.Globalization;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing.Forge;

/// <summary>A pull request held by <see cref="FakePullRequests"/>.</summary>
/// <param name="repo">The repository.</param>
/// <param name="number">The number in the repository.</param>
/// <param name="head">The head branch.</param>
/// <param name="headSha">The head's tip.</param>
public sealed class FakePull(PullRepo repo, int number, string head, string headSha)
{
    /// <summary>Gets the repository.</summary>
    public PullRepo Repo { get; } = repo;

    /// <summary>Gets the number in the repository.</summary>
    public int Number { get; } = number;

    /// <summary>Gets the head branch.</summary>
    public string Head { get; } = head;

    /// <summary>Gets or sets the head's tip.</summary>
    public string HeadSha { get; set; } = headSha;

    /// <summary>Gets or sets where the pull request is in its life.</summary>
    public PullState State { get; set; } = PullState.Open;

    /// <summary>Gets or sets the author's login.</summary>
    public string Author { get; set; } = "sindri";

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets whether it is a draft.</summary>
    public bool Draft { get; set; }

    /// <summary>Gets or sets the labels.</summary>
    public IReadOnlyList<string> Labels { get; set; } = [];

    /// <summary>Gets or sets the base branch (main until Open or Retarget says otherwise).</summary>
    public string Base { get; set; } = "main";

    /// <summary>Gets or sets the base branch's tip now; null when the fake forge says none.</summary>
    public string? BaseSha { get; set; }

    /// <summary>Gets or sets the marker the pull request was opened with.</summary>
    public string? Marker { get; set; }

    /// <summary>Gets or sets whether the forge says it merges cleanly; null while it computes.</summary>
    public bool? Mergeable { get; set; } = true;

    /// <summary>Gets or sets the review decision.</summary>
    public string? ReviewDecision { get; set; }

    /// <summary>Gets or sets the checks' state.</summary>
    public PullChecksState ChecksState { get; set; } = PullChecksState.None;

    /// <summary>Gets or sets the commit the checks ran on; null means the head.</summary>
    public string? ChecksSha { get; set; }

    /// <summary>Gets the names of the failing checks.</summary>
    public IList<string> FailingChecks { get; } = [];

    /// <summary>Gets or sets the checks; null reads as none, on the head's tip.</summary>
    public PullChecks? Checks { get; set; }

    /// <summary>Gets the threads, oldest first.</summary>
    public IList<PullThread> Threads { get; } = [];

    /// <summary>Gets or sets the changed files.</summary>
    public IReadOnlyList<PullFile> Files { get; set; } = [];

    /// <summary>Gets the text of each file at the head, by path.</summary>
    public IDictionary<string, string> FileTexts { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the text of each file at the base, by the path the file had there (its previous path after a rename).</summary>
    public IDictionary<string, string> BaseTexts { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets whether the pull request has more files than the forge lists.</summary>
    public bool FilesCapReached { get; set; }

    /// <summary>Gets the reviews posted, by marker.</summary>
    public IDictionary<string, PullReview> Reviews { get; } = new Dictionary<string, PullReview>(StringComparer.Ordinal);

    /// <summary>Gets the pull request's link.</summary>
    public string Link => string.Create(CultureInfo.InvariantCulture, $"https://{Repo.Host}/{Repo.Owner}/{Repo.Name}/pull/{Number}");
}
