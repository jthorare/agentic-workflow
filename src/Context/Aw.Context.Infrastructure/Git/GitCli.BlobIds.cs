using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

internal sealed partial class GitCli
{
    /// <summary>The blob id of each of <paramref name="paths"/> at <paramref name="commit"/>, from one 'ls-tree'. A path that is not a file there (missing, a directory, a submodule) is left out.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A commit sha.</param>
    /// <param name="paths">Repo-relative, '/'-separated paths; the caller has checked them (relative, no '..', no leading ':').</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Path to blob sha, or SND-CONTEXT-010 when git can't read the commit.</returns>
    public async ValueTask<Result<IReadOnlyDictionary<string, string>>> BlobIds(string repo, string commit, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var wanted = paths.ToHashSet(StringComparer.Ordinal);
        if (wanted.Count == 0)
        {
            return Results.Success<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.Ordinal));
        }

        // why: a path after '--' is a literal prefix to ls-tree, so the answer is filtered back to exactly the paths asked for
        var listed = await Text(repo, ["ls-tree", "-z", "--full-tree", commit, "--", .. wanted], cancellationToken).ConfigureAwait(false);
        return listed.Map<IReadOnlyDictionary<string, string>>(text => text.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split('\t', 2))
            .Where(parts => parts.Length == 2 && wanted.Contains(parts[1]) && parts[0].Split(' ') is [_, "blob", _])
            .ToDictionary(parts => parts[1], parts => parts[0].Split(' ')[2], StringComparer.Ordinal));
    }
}
