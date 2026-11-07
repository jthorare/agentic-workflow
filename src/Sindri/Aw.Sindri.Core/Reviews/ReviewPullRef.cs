using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Reviews;

/// <summary>A PR as the review cache names it. The segments are checked against GitHub's name rules before they build a path.</summary>
/// <param name="Owner">The account.</param>
/// <param name="Repo">The repository name.</param>
/// <param name="Number">The PR number.</param>
public sealed partial record ReviewPullRef(string Owner, string Repo, int Number)
{
    /// <summary>Reads "owner/name".</summary>
    /// <param name="repo">The repo as the list holds it.</param>
    /// <param name="number">The PR number.</param>
    /// <returns>The reference, or null when a segment is not a valid GitHub name.</returns>
    public static ReviewPullRef? TryParse(string repo, int number)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var parts = repo.Split('/');
        return parts is [var owner, var name] && number > 0 && IsOwner(owner) && IsRepo(name) ? new ReviewPullRef(owner, name, number) : null;
    }

    /// <summary>Whether <paramref name="sha"/> is a commit id fit to be a folder name.</summary>
    /// <param name="sha">The commit.</param>
    /// <returns>True for 7 to 64 hex digits.</returns>
    public static bool IsSha(string sha) => sha is not null && ShaName.IsMatch(sha);

    /// <summary>Whether the three segments pass GitHub's name rules.</summary>
    public bool IsValid => Number > 0 && IsOwner(Owner) && IsRepo(Repo);

    private static bool IsOwner(string owner) => OwnerName.IsMatch(owner);

    private static bool IsRepo(string repo) => repo is not ("." or "..") && RepoName.IsMatch(repo);

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OwnerName { get; }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoName { get; }

    [GeneratedRegex("^[0-9a-fA-F]{7,64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ShaName { get; }
}
