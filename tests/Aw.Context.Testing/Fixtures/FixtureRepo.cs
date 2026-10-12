namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// The fixture app built as a git repo in a temp directory, with the merged-PR history of <see cref="SplitPanesFixture.History"/>:
/// an initial commit, then per pull request one commit '&lt;KEY&gt;: &lt;title&gt;' on its branch and a no-fast-forward merge
/// 'Merge pull request #N from &lt;owner&gt;/&lt;branch&gt;'. Deterministic: the same fixture gives the same shas on every
/// machine. Dispose deletes it.
/// </summary>
public sealed class FixtureRepo : IDisposable
{
    private readonly string _root;

    private FixtureRepo(string root, string path, string head, IReadOnlyList<MergedPullRequest> pullRequests)
    {
        _root = root;
        Path = path;
        Head = head;
        PullRequests = pullRequests;
    }

    /// <summary>The repo's working tree (branch 'main' checked out, clean).</summary>
    public string Path { get; }

    /// <summary>The sha of 'main'.</summary>
    public string Head { get; }

    /// <summary>The merged pull requests, oldest first.</summary>
    public IReadOnlyList<MergedPullRequest> PullRequests { get; }

    /// <summary>Builds the repo.</summary>
    /// <param name="parentDirectory">
    /// Where to create it: its new subfolder 'aw-split-panes', which must not exist yet (Dispose deletes it). A new temp
    /// directory when null.
    /// </param>
    /// <returns>The built repo.</returns>
    public static FixtureRepo Build(string? parentDirectory = null)
    {
        var root = parentDirectory is null
            ? Directory.CreateTempSubdirectory("aw-split-panes-").FullName
            : NewFolder(System.IO.Path.Combine(parentDirectory, "aw-split-panes"));
        var built = false;
        try
        {
            var repo = Create(root);
            built = true;
            return repo;
        }
        finally
        {
            if (!built)
            {
                TryDelete(root);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // Cleanup after a failed build must not replace the build's own error (git's stderr) with a delete failure.
    private static void TryDelete(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // left behind; the build error that brought us here is the one to report
        }
    }

    private static string NewFolder(string path) =>
        Directory.Exists(path) || File.Exists(path)
            ? throw new InvalidOperationException($"'{path}' already exists; build the fixture repo into a parent without one.")
            : Directory.CreateDirectory(path).FullName;

    private static FixtureRepo Create(string root)
    {
        var history = SplitPanesFixture.History;
        var path = Directory.CreateDirectory(System.IO.Path.Combine(root, "split-panes")).FullName;
        var emptyConfig = System.IO.Path.Combine(root, "gitconfig");
        File.WriteAllText(emptyConfig, "");
        var git = new GitRunner(path, emptyConfig, history.Author);
        var initial = history.Initial;
        _ = git.Run(initial.At, "init", "-q", "-b", "main");
        _ = git.Run(initial.At, "config", "core.hooksPath", System.IO.Path.Combine(root, "no-hooks"));
        _ = Write(path, initial.Dir);
        Commit(git, initial.At, initial.Message);
        var merged = history.PullRequests.Select(pr => Merge(git, path, history.Repository, pr)).ToList();
        return new FixtureRepo(root, path, git.Run(initial.At, "rev-parse", "main"), merged);
    }

    private static MergedPullRequest Merge(GitRunner git, string path, string repository, FixturePullRequest pr)
    {
        _ = git.Run(pr.CommittedAt, "checkout", "-q", "-b", pr.Branch);
        var files = Write(path, pr.Dir);
        Commit(git, pr.CommittedAt, $"{pr.IssueKey}: {pr.Title}");
        _ = git.Run(pr.MergedAt, "checkout", "-q", "main");
        var message = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Merge pull request #{pr.Number} from {repository.Split('/')[0]}/{pr.Branch}");
        _ = git.Run(pr.MergedAt, "merge", "-q", "--no-ff", "--no-edit", "-m", message, "-m", pr.Title, pr.Branch);
        var sha = git.Run(pr.MergedAt, "rev-parse", "HEAD");
        return new MergedPullRequest(pr.Number, pr.IssueKey, pr.Branch, pr.Title, sha, pr.MergedAt, files);
    }

    private static void Commit(GitRunner git, DateTimeOffset at, string message)
    {
        _ = git.Run(at, "add", "-A");
        _ = git.Run(at, "commit", "-q", "--no-gpg-sign", "-m", message);
    }

    private static List<string> Write(string path, string folder)
    {
        var files = SplitPanesFixture.FilesUnder(folder);
        foreach (var (relative, resource) in files)
        {
            var target = System.IO.Path.Combine(path, relative);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!); // why: a file under the repo has a directory
            File.WriteAllBytes(target, SplitPanesFixture.ReadBytes(resource));
        }

        return [.. files.Select(f => f.Key)];
    }
}
