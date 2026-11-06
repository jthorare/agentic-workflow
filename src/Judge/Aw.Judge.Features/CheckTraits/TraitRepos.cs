using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// The repos a check judges. The repo is the file's, not the working directory's: an agent started in one repo may edit
/// another's files by absolute path. An edit groups its files by the repo each lies in and records each repo under the
/// session (<see cref="ITraitCheckSessions"/>); a stop judges the working directory's repo and every repo the session's
/// edits recorded; a scan the working directory's. What is passed over leaves a 'skipped' line in the log.
/// </summary>
/// <param name="git">git through the process runner.</param>
/// <param name="files">The working directory and the file system.</param>
/// <param name="log">The trait-check log.</param>
/// <param name="sessions">The repos each session edited files in.</param>
internal sealed class TraitRepos(TraitGit git, ITraitFiles files, ITraitCheckLog log, ITraitCheckSessions sessions)
{
    internal async ValueTask<(string? Home, List<RepoFiles> Repos)> For(CheckTraitsRequest request, CancellationToken cancellationToken)
    {
        var home = (await Locate(files.WorkingDirectory, cancellationToken).ConfigureAwait(false))?.Root;
        if (request.Mode != TraitCheckMode.Edit && home is null)
        {
            Skipped(request, ("path", files.WorkingDirectory), "not-in-a-repo");
        }

        var repos = request.Mode switch
        {
            TraitCheckMode.Edit => await Edited(request, cancellationToken).ConfigureAwait(false),
            TraitCheckMode.Stop => await Stopped(request, home, cancellationToken).ConfigureAwait(false),
            TraitCheckMode.Scan or _ => Only(home),
        };
        return (home, repos);
    }

    // A repo or a file the check passed over, and why: the log is where a check that judged nothing shows.
    internal void Skipped(CheckTraitsRequest request, (string Key, string Value) what, string reason) => log.Append(new JsonObject
    {
        ["event"] = "skipped",
        ["mode"] = request.Mode.ToString().ToLowerInvariant(),
        [what.Key] = what.Value,
        ["reason"] = reason,
    });

    // A directory's repo: its top-level directory and the directory's path inside it ('src/', or '' at the top), both as
    // git prints them, so a file's repo-relative path never depends on how its absolute path spells a symlink (macOS's
    // /tmp is /private/tmp). Null outside a repo, or in a directory git can't run in.
    private async ValueTask<(string Root, string Prefix)?> Locate(string directory, CancellationToken cancellationToken)
    {
        var (code, output) = await git.Run(["rev-parse", "--show-toplevel", "--show-prefix"], directory, cancellationToken).ConfigureAwait(false);
        var lines = output.Split('\n');
        return code == 0 && lines[0].Length > 0 ? (lines[0], lines.Length > 1 ? lines[1] : "") : null;
    }

    // Edit: each file's repo, found from its directory (or, when that is gone or not made yet, the nearest parent that
    // exists), files grouped by repo in the order first edited. A file in no repo is passed over.
    private async ValueTask<List<RepoFiles>> Edited(CheckTraitsRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, (string Root, string Prefix)?> located = new(StringComparer.Ordinal);
        List<RepoFiles> repos = [];
        foreach (var full in request.Files.Select(file => Path.GetFullPath(file, files.WorkingDirectory)))
        {
            var directory = Existing(Path.GetDirectoryName(full));
            if (directory is not null && !located.ContainsKey(directory))
            {
                located[directory] = await Locate(directory, cancellationToken).ConfigureAwait(false);
            }

            if (directory is null || located[directory] is not ({ } root, { } prefix))
            {
                Skipped(request, ("path", full), "not-in-a-repo");
                continue;
            }

            Add(request, repos, root, prefix + Path.GetRelativePath(directory, full).Replace('\\', '/'));
        }

        return repos;
    }

    private static List<RepoFiles> Only(string? home) => home is null ? [] : [new RepoFiles(home, [])];

    private string? Existing(string? directory)
    {
        while (directory is not null && !files.DirectoryExists(directory))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory;
    }

    // A repo seen for the first time is recorded under the session, for its stop.
    private void Add(CheckTraitsRequest request, List<RepoFiles> repos, string root, string relative)
    {
        if (repos.Find(repo => string.Equals(repo.Root, root, StringComparison.Ordinal)) is { } known)
        {
            known.Files.Add(relative);
            return;
        }

        repos.Add(new RepoFiles(root, [relative]));
        if (request.SessionId is { } session)
        {
            sessions.Add(session, root);
        }
    }

    // Stop: the working directory's repo, then each repo the session's edits recorded that is still a repo's top.
    private async ValueTask<List<RepoFiles>> Stopped(CheckTraitsRequest request, string? home, CancellationToken cancellationToken)
    {
        var repos = Only(home);
        foreach (var recorded in request.SessionId is { } session ? sessions.Repos(session) : [])
        {
            if (files.DirectoryExists(recorded)
                && await Locate(recorded, cancellationToken).ConfigureAwait(false) is ({ } root, "")
                && !repos.Exists(repo => string.Equals(repo.Root, root, StringComparison.Ordinal)))
            {
                repos.Add(new RepoFiles(root, []));
            }
        }

        return repos;
    }
}
