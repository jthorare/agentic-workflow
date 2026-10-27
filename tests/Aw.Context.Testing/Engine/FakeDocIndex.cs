using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory <see cref="IDocIndex"/>: a refresh reads the repo's working tree (not git), keeps the files the globs
/// match, and compares them with the last refresh by content hash and scrub ruleset: new or changed files, and files
/// stored under another ruleset (counted as re-cleaned), are added, missing ones removed, the rest kept. One chunk per document. It passes <see cref="DocIndexContract{TIndex}"/>.
/// </summary>
/// <param name="clock">The clock it stamps refreshes with.</param>
public sealed class FakeDocIndex(TimeProvider clock) : IDocIndex
{
    private readonly Dictionary<ContextRepoName, (DocIndexStatus Status, Dictionary<string, (string Hash, ScrubRuleset Ruleset)> Docs)> _repos = [];

    /// <summary>When set, Refresh answers this code for every repo (a repo that can't be read).</summary>
    public string? RefreshCode { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<RepoRefresh>> Refresh(ContextRepo repo, IReadOnlyList<string> globs, Scrubbers scrubbers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(globs);
        ArgumentNullException.ThrowIfNull(scrubbers);
        var known = _repos.TryGetValue(repo.Name, out var entry)
            ? entry
            : (Status: new DocIndexStatus(Head: null, LastSync: null, Code: null, 0, 0), Docs: new Dictionary<string, (string Hash, ScrubRuleset Ruleset)>(StringComparer.Ordinal));
        if (RefreshCode is { } code)
        {
            _repos[repo.Name] = (known.Status with { Code = code }, known.Docs);
            return ValueTask.FromResult<Result<RepoRefresh>>(new RepoRefresh(repo.Name, Head: null, 0, 0, 0, 0, code));
        }

        var docs = Read(repo.Path, globs);
        var added = docs.Count(d => !known.Docs.TryGetValue(d.Key, out var stored) || !string.Equals(stored.Hash, d.Value, StringComparison.Ordinal) || stored.Ruleset != scrubbers.Ruleset);
        var recleaned = docs.Count(d => known.Docs.TryGetValue(d.Key, out var stored) && string.Equals(stored.Hash, d.Value, StringComparison.Ordinal) && stored.Ruleset != scrubbers.Ruleset);
        var removed = known.Docs.Keys.Count(k => !docs.ContainsKey(k));
        var head = SourceVersion.From("fake-" + Hash(string.Join('\n', docs.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => d.Key + d.Value)))[..12]);
        _repos[repo.Name] = (new DocIndexStatus(head, clock.GetUtcNow(), Code: null, docs.Count, docs.Count),
            docs.ToDictionary(d => d.Key, d => (d.Value, scrubbers.Ruleset), StringComparer.Ordinal));
        return ValueTask.FromResult<Result<RepoRefresh>>(new RepoRefresh(repo.Name, head, added, removed, docs.Count - added, Blocked: 0, Code: null) { Recleaned = recleaned });
    }

    /// <inheritdoc />
    public ValueTask<Result<DocIndexStatus>> Status(ContextRepo repo, ScrubRuleset ruleset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return ValueTask.FromResult<Result<DocIndexStatus>>(_repos.TryGetValue(repo.Name, out var entry)
            ? entry.Status with { Outdated = entry.Docs.Values.Count(d => d.Ruleset != ruleset) }
            : new DocIndexStatus(Head: null, LastSync: null, Code: null, 0, 0));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<IndexedDoc>>> Read(ContextRepo repo, CancellationToken cancellationToken) => throw new NotSupportedException();

    private static Dictionary<string, string> Read(string root, IReadOnlyList<string> globs)
    {
        var patterns = globs.Select(g => new Regex(ToRegex(g), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))).ToList();
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => (Path: Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'), File: f))
                .Where(f => !f.Path.StartsWith(".git/", StringComparison.Ordinal) && patterns.Exists(p => p.IsMatch(f.Path)))
                .ToDictionary(f => f.Path, f => Hash(File.ReadAllText(f.File)), StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static string ToRegex(string glob) =>
        "^" + Regex.Escape(glob).Replace("\\*\\*/", "(?:[^/]+/)*", StringComparison.Ordinal).Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal) + "\\z";

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
}
