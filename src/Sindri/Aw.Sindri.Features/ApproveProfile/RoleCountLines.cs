using System.Globalization;
using Aw.Context.Core.Roles;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>
/// For each changed 'index.roles.&lt;role&gt;' or 'index.demote' key of a repo, how many files the role matches among the files git tracks there (#1148). Read-only and bounded: the file list is read once
/// per repo, under <see cref="Budget"/> and capped at <see cref="FileCap"/> files, and any failure says 'count unavailable' instead of slowing the preview.
/// </summary>
internal static class RoleCountLines
{
    internal const int FileCap = 200_000;

    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    /// <summary>The preview lines for the changed role keys; none when no role key changed.</summary>
    /// <param name="tracked">Lists a repo's files; null when this build can't (every line then says the count is unavailable).</param>
    /// <param name="previous">The latest approved profile, if any.</param>
    /// <param name="live">The live profile.</param>
    /// <param name="changes">The changed key paths.</param>
    /// <param name="cancellationToken">Cancels the preview.</param>
    /// <returns>Lines such as 'repos.web.index.roles.test: 412 files on HEAD (was 0)', in repo and key order.</returns>
    public static async ValueTask<IReadOnlyList<string>> Of(ITrackedFiles? tracked, SindriProfile? previous, SindriProfile live, IReadOnlyList<string> changes, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        foreach (var repo in live.Repos ?? [])
        {
            var before = previous?.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo.Name, StringComparison.Ordinal))?.Index;
            var keys = Keys(repo.Name, before, repo.Index).Where(k => changes.Contains(k.Changed, StringComparer.Ordinal)).ToList();
            if (keys.Count == 0)
            {
                continue;
            }

            var paths = await Listing(tracked, repo.Path, cancellationToken).ConfigureAwait(false);
            lines.AddRange(keys.Select(k => paths is null
                ? $"{k.Key}: count unavailable"
                : string.Create(CultureInfo.InvariantCulture, $"{k.Key}: {Count(k.Globs, paths)} files on HEAD (was {Count(k.WasGlobs, paths)})")));
        }

        return lines;
    }

    /// <summary>One entry per role or demoted role either side names: its key path and the globs it covers now and before (none when it isn't there).</summary>
    private static IEnumerable<(string Key, string Changed, IReadOnlyList<string> Globs, IReadOnlyList<string> WasGlobs)> Keys(string repo, IndexSettings? before, IndexSettings? now)
    {
        foreach (var role in Names(before?.Roles?.Keys, now?.Roles?.Keys))
        {
            var key = $"repos.{repo}.index.roles.{role}";
            yield return (key, key, Globs(now, role), Globs(before, role));
        }

        // the profile diff lists the whole demote list as one key ('repos.web.index.demote'), so a role is shown when it joined or left that list
        foreach (var role in Names(before?.Demote, now?.Demote).Where(r => IsDemoted(before, r) != IsDemoted(now, r)))
        {
            yield return ($"repos.{repo}.index.demote.{role}", $"repos.{repo}.index.demote", Demoted(now, role), Demoted(before, role));
        }
    }

    private static IEnumerable<string> Names(IEnumerable<string>? before, IEnumerable<string>? now) =>
        (before ?? []).Concat(now ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private static IReadOnlyList<string> Globs(IndexSettings? index, string role) =>
        index?.Roles is { } roles && roles.TryGetValue(role, out var globs) ? globs : [];

    private static bool IsDemoted(IndexSettings? index, string role) => index?.Demote?.Contains(role, StringComparer.Ordinal) == true;

    private static IReadOnlyList<string> Demoted(IndexSettings? index, string role) =>
        IsDemoted(index, role) ? Globs(index, role) : [];

    private static int Count(IReadOnlyList<string> globs, IReadOnlyList<string> paths) => globs.Count == 0 ? 0 : RoleCounter.Matching(globs, paths);

    /// <summary>The repo's tracked files, or null when git can't list them in time or there are more than <see cref="FileCap"/>.</summary>
    private static async ValueTask<IReadOnlyList<string>?> Listing(ITrackedFiles? tracked, string path, CancellationToken cancellationToken)
    {
        if (tracked is null)
        {
            return null;
        }

        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(Budget);
        try
        {
            var listed = await tracked.Paths(path, bound.Token).ConfigureAwait(false);
            return listed.IsSuccess && listed.Value.Count <= FileCap ? listed.Value : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;   // the budget ran out, not the caller's cancel
        }
    }
}
