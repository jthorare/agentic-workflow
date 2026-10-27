using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Testing;

/// <summary>
/// The in-memory fake for <see cref="IGit"/> and <see cref="IFiles"/>: one file system (full path → text) holding work
/// trees, and commits as snapshots of a work tree's files. They are one fake because git answers about the same files
/// the file-system port reads.
/// </summary>
public sealed class InMemoryRepos : IGit, IFiles
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Snapshot> _commits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Worktree> _worktrees = new(StringComparer.Ordinal);
    private int _next;

    /// <summary>Creates a repository at <paramref name="root"/> on branch main with one commit of <paramref name="files"/>.</summary>
    /// <param name="root">The work tree's full path.</param>
    /// <param name="files">Repository-relative path → content.</param>
    /// <returns>The first commit.</returns>
    public string Init(string root, IReadOnlyDictionary<string, string> files)
    {
        _worktrees[root] = new Worktree(root, "main", Head: string.Empty);
        foreach (var (path, text) in files)
        {
            _files[$"{root}/{path}"] = text;
        }

        return Commit(root);
    }

    /// <summary>Commits every file of the work tree (git add -A; git commit).</summary>
    /// <param name="root">The work tree.</param>
    /// <param name="message">The commit message.</param>
    /// <returns>The new commit.</returns>
    public string Commit(string root, string message = "")
    {
        var tree = _worktrees[root];
        var sha = (++_next).ToString("x40", CultureInfo.InvariantCulture);
        _commits[sha] = new Snapshot(tree.Head, Working(root), message);
        _worktrees[root] = tree with { Head = sha };
        return sha;
    }

    /// <summary>Adds a work tree of the same repository on a new branch at <paramref name="commit"/> (git worktree add -b).</summary>
    /// <param name="root">The new work tree's full path.</param>
    /// <param name="branch">The new branch.</param>
    /// <param name="commit">The commit to start from.</param>
    public void AddWorktree(string root, string branch, string commit)
    {
        _worktrees[root] = new Worktree(root, branch, commit);
        foreach (var (path, text) in _commits[commit].Files)
        {
            _files[$"{root}/{path}"] = text;
        }
    }

    /// <summary>Writes a file (a working change until committed).</summary>
    /// <param name="path">The full path.</param>
    /// <param name="text">The content.</param>
    public void Write(string path, string text) => _files[path] = text;

    /// <summary>Makes <paramref name="link"/> (a file or a directory) resolve to <paramref name="target"/>, like a symlink.</summary>
    /// <param name="link">The link's full path.</param>
    /// <param name="target">The target: a full path, or one relative to the link's directory.</param>
    public void Link(string link, string target) => _links[link] = target;

    /// <inheritdoc/>
    public string FullPath(string directory, string path) => path.StartsWith('/') ? path : $"{directory.TrimEnd('/')}/{path}";

    /// <inheritdoc/>
    public string? RealPath(string path)
    {
        var real = path.StartsWith('/') ? Resolve(path, 40) : null;
        return real is not null && _files.ContainsKey(real) ? real : null;
    }

    /// <inheritdoc/>
    public Result<string> RelativeDirectory(string directory) =>
        Tree(directory).Map(t => directory.Length > t.Root.Length ? directory[(t.Root.Length + 1)..].TrimEnd('/') : string.Empty);

    /// <inheritdoc/>
    public bool Exists(string path)
    {
        var resolved = path.StartsWith('/') ? Resolve(path, 40) : null;
        return resolved is not null && (_files.ContainsKey(resolved) || _files.Keys.Any(f => f.StartsWith(resolved.TrimEnd('/') + "/", StringComparison.Ordinal)));
    }

    /// <inheritdoc/>
    public Result<string> ReadText(string path) =>
        _files.TryGetValue(path, out var text) ? text : BugFixErrors.FileUnreadable.With($"cannot read {path}");

    /// <inheritdoc/>
    public Result<string> Sha256(string path) =>
        _files.TryGetValue(path, out var text)
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            : BugFixErrors.FileUnreadable.With($"cannot read {path}");

    /// <inheritdoc/>
    public Result<string> Head(string directory) => Tree(directory).Map(static t => t.Head);

    /// <inheritdoc/>
    public Result<string> Branch(string directory) => Tree(directory).Map(static t => t.Branch);

    /// <inheritdoc/>
    public Result<bool> IsClean(string directory) =>
        Tree(directory).Map(t =>
        {
            var working = Working(t.Root);
            return _commits[t.Head].Files.All(kv => working.TryGetValue(kv.Key, out var text) && string.Equals(text, kv.Value, StringComparison.Ordinal));
        });

    /// <inheritdoc/>
    public Result<IReadOnlyList<string>> Untracked(string directory) =>
        Tree(directory).Map(t =>
        {
            var working = Working(t.Root);
            var ignored = (working.GetValueOrDefault(".gitignore") ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return (IReadOnlyList<string>)[.. working.Keys
                .Where(f => !_commits[t.Head].Files.ContainsKey(f) && !Array.Exists(ignored, i => i.EndsWith('/') ? f.StartsWith(i, StringComparison.Ordinal) : f == i))
                .Order(StringComparer.Ordinal)];
        });

    /// <summary>Removes a file from the work tree (rm).</summary>
    /// <param name="path">The full path.</param>
    public void Delete(string path) => _files.Remove(path);

    /// <inheritdoc/>
    public Result<string> Root(string directory) => Tree(directory).Map(static t => t.Root);

    /// <inheritdoc/>
    public Result<bool> IsCommittedUnchanged(string root, string file) =>
        Tree(root).Map(t => _commits[t.Head].Files.TryGetValue(file, out var committed)
            && _files.TryGetValue($"{t.Root}/{file}", out var working)
            && string.Equals(committed, working, StringComparison.Ordinal));

    /// <inheritdoc/>
    public Result<bool> IsAncestor(string directory, string ancestor, string commit)
    {
        for (var at = commit; at.Length > 0; at = _commits[at].Parent)
        {
            if (string.Equals(at, ancestor, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public Result<bool> SameTree(string directory, string first, string second) =>
        Same(_commits[first].Files, _commits[second].Files);

    /// <summary>The tree-ish <see cref="NetBase"/> returns instead of the baseline; null keeps the baseline.</summary>
    public string? NetBaseOverride { get; set; }

    /// <inheritdoc/>
    public string NetBase(string directory, string baseline, string candidate) => NetBaseOverride ?? baseline;

    /// <inheritdoc/>
    public Result<IReadOnlyList<string>> ChangedFiles(string directory, string from, string to)
    {
        var (a, b) = (_commits[from].Files, _commits[to].Files);
        return Results.Success<IReadOnlyList<string>>([.. a.Keys.Union(b.Keys, StringComparer.Ordinal)
            .Where(f => !a.TryGetValue(f, out var x) || !b.TryGetValue(f, out var y) || !string.Equals(x, y, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)]);
    }

    /// <inheritdoc/>
    public Result<IReadOnlyList<string>> AddedFiles(string directory, string from, string to) =>
        Results.Success<IReadOnlyList<string>>([.. _commits[to].Files.Keys.Where(f => !_commits[from].Files.ContainsKey(f)).Order(StringComparer.Ordinal)]);

    /// <inheritdoc/>
    public Result<CommitDiff> Diff(string directory, string from, string to) =>
        ChangedFiles(directory, from, to).Map(files => new CommitDiff(
            string.Create(CultureInfo.InvariantCulture, $" {files.Count} files changed"),
            string.Concat(files.Select(f => $"diff --git a/{f} b/{f}\n+{_commits[to].Files.GetValueOrDefault(f, string.Empty)}\n"))));

    /// <inheritdoc/>
    /// <remarks>Main is the work tree on branch main: its commits since <paramref name="baseline"/>, oldest first.</remarks>
    public Result<IReadOnlyList<MainCommit>> MainCommits(string directory, string baseline, string candidate)
    {
        var log = new List<MainCommit>();
        var main = _worktrees.Values.FirstOrDefault(static t => t.Branch == "main");
        for (var at = main?.Head ?? string.Empty; at.Length > 0 && !IsAncestor(directory, at, baseline).Value; at = _commits[at].Parent)
        {
            log.Insert(0, new MainCommit(at, _commits[at].Parent, _commits[at].Message));
        }

        return Results.Success<IReadOnlyList<MainCommit>>(log);
    }

    /// <inheritdoc/>
    public Result<string> FileAt(string directory, string commit, string path) =>
        _commits[commit].Files.TryGetValue(path, out var text) ? text : BugFixErrors.GitFailed.With($"fatal: path '{path}' does not exist in '{commit}'");

    /// <inheritdoc/>
    public Result<IReadOnlyList<string>> FilesDeclaring(string directory, string commit, string under, string typeName)
    {
        var prefix = under.Length == 0 ? string.Empty : under.TrimEnd('/') + "/";
        var declares = new Regex($@"(^|[^A-Za-z0-9_])(class|record|struct|interface)\s+{Regex.Escape(typeName)}([^A-Za-z0-9_]|$)", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return Results.Success<IReadOnlyList<string>>([.. _commits[commit].Files
            .Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal) && declares.IsMatch(f.Value))
            .Select(static f => f.Key)
            .Order(StringComparer.Ordinal)]);
    }

    // Like realpath(3): each link resolved before the next component, ".." applied to the resolved directory.
    private string? Resolve(string path, int budget)
    {
        var current = string.Empty;
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                current = current.Length == 0 ? current : current[..current.LastIndexOf('/')];
            }
            else if (part != ".")
            {
                current = $"{current}/{part}";
                if (_links.TryGetValue(current, out var target))
                {
                    var resolved = budget == 0 ? null : Resolve(target.StartsWith('/') ? target : $"{current[..current.LastIndexOf('/')]}/{target}", budget - 1);
                    if (resolved is null)
                    {
                        return null;
                    }

                    current = resolved;
                }
            }
        }

        return current.Length == 0 ? "/" : current;
    }

    private static bool Same(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && string.Equals(v, kv.Value, StringComparison.Ordinal));

    private Result<Worktree> Tree(string directory) =>
        _worktrees.Values.FirstOrDefault(t => directory == t.Root || directory.StartsWith(t.Root + "/", StringComparison.Ordinal)) is { } tree
            ? tree
            : BugFixErrors.GitFailed.With($"fatal: not a git repository: {directory}");

    private Dictionary<string, string> Working(string root) =>
        _files.Where(kv => kv.Key.StartsWith(root + "/", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key[(root.Length + 1)..], kv => kv.Value, StringComparer.Ordinal);

    private sealed record Snapshot(string Parent, IReadOnlyDictionary<string, string> Files, string Message);

    private sealed record Worktree(string Root, string Branch, string Head);
}
