using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// <see cref="IT3RepoIdentities"/> over git, hardened like every git call on a folder Sindri didn't pick itself: the main checkout from the common git
/// directory, the remotes from 'git remote -v'. A folder git can't read gives nulls, never an error.
/// </summary>
internal sealed class GitT3RepoIdentities : IT3RepoIdentities
{
    /// <inheritdoc/>
    public async ValueTask<T3RepoIdentity> Of(string path, CancellationToken cancellationToken)
    {
        var common = await GitProcess.Run(path, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken, hardened: true).ConfigureAwait(false);
        var remotes = await GitProcess.Run(path, ["remote", "-v"], cancellationToken, hardened: true).ConfigureAwait(false);
        var main = common.Exit == 0 && Path.GetFileName(Path.TrimEndingDirectorySeparator(common.Output)) == ".git" ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(common.Output)) : null;
        return new T3RepoIdentity(main, remotes.Exit == 0 ? T3RemoteKeys.Primary(Fetches(remotes.Output)) : null);
    }

    /// <summary>Each remote's fetch URL from 'git remote -v' ('origin\tgit@…\t(fetch)').</summary>
    private static Dictionary<string, string> Fetches(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries))
            .Where(static parts => parts is [_, _, "(fetch)"])
            .GroupBy(static parts => parts[0], StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First()[1], StringComparer.Ordinal);
}
