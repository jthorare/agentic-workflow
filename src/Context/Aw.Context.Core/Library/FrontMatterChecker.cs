using System.Text.RegularExpressions;

namespace Aw.Context.Core.Library;

/// <summary>Checks the keys of a design doc's front matter against the convention (SND-LIB-010): one error at most per key.</summary>
internal sealed partial class FrontMatterChecker
{
    private static readonly string[] _kinds = ["tdd", "spec", "plan", "stages", "product", "engineering", "design", "tasks", "verification", "note"];
    private static readonly string[] _statuses = ["draft", "review", "approved", "building", "done", "superseded"];
    private const string LinkText = "a doc id or a full doc:<root>[/<slug>]/<doc id> ref";

    private readonly string _path;
    private readonly int _openLine;
    private readonly Dictionary<string, YamlEntry> _byKey = new(StringComparer.Ordinal);
    private readonly List<DesignDocError> _errors = [];
    private readonly HashSet<string> _bad = new(StringComparer.Ordinal);

    private FrontMatterChecker(string path, IReadOnlyList<YamlEntry> entries, int openLine)
    {
        _path = path;
        _openLine = openLine;
        foreach (var entry in entries)
        {
            _ = _byKey.TryAdd(entry.Key, entry);
        }
    }

    /// <summary>Checks the entries.</summary>
    /// <param name="path">The file, for messages.</param>
    /// <param name="entries">The keys read.</param>
    /// <param name="openLine">The line of the opening delimiter (where a missing key is reported).</param>
    /// <returns>The front matter as written and its errors.</returns>
    internal static (DesignDocFrontMatter Doc, List<DesignDocError> Errors) Check(string path, IReadOnlyList<YamlEntry> entries, int openLine)
    {
        var checker = new FrontMatterChecker(path, entries, openLine);
        var doc = checker.Run();
        return (doc, checker._errors);
    }

    internal static bool IsDocId(string text) => DocIdShape.IsMatch(text);

    private DesignDocFrontMatter Run()
    {
        var id = Text("id", required: true, DocIdShape, "3 to 64 lowercase letters, digits or '-'");
        var kind = OneOf("kind", _kinds);
        var title = Text("title", required: true, shape: null, "a title");
        var status = OneOf("status", _statuses);
        var project = Text("project", required: false, ProjectShape, "a Sindri project id (prj_...) or linear:project/<id>");
        var supersedes = Text("supersedes", required: false, DocLinkShape, LinkText);
        var parent = Text("parent", required: false, DocLinkShape, LinkText);
        var generated = Text("generated", required: false, shape: null, "the tool that wrote the file");
        var sha = Text("source-sha256", required: false, Sha256Shape, "64 lowercase hex digits");
        CheckGeneratedPair(generated, sha);
        var repos = List("repos", static v => RepoShape.IsMatch(v.Value), "owner/name entries");
        var issues = List("issues", static v => (!v.Quoted && IssueNumberShape.IsMatch(v.Value)) || IssueKeyShape.IsMatch(v.Value), "plain issue numbers (412) or Linear keys (ENG-12)");
        var lines = _byKey.ToDictionary(static p => p.Key, static p => p.Value.Line, StringComparer.Ordinal);
        return new DesignDocFrontMatter(1, id, kind, title, status, project, repos, issues, supersedes, parent, generated, sha) { KeyLines = lines };
    }

    private void Fail(string key, string what, string fix)
    {
        if (_bad.Add(key))
        {
            var line = _byKey.TryGetValue(key, out var entry) ? entry.Line : _openLine;
            _errors.Add(FrontMatterBlock.Error(_path, line, DesignDocCodes.BadFrontMatter, what, fix));
        }
    }

    private string? Text(string key, bool required, Regex? shape, string describe)
    {
        if (!_byKey.TryGetValue(key, out var entry))
        {
            if (required)
            {
                Fail(key, $"the key {key} is missing.", $"Add {key}: {describe}.");
            }

            return null;
        }

        if (entry.Scalar is not { } scalar)
        {
            Fail(key, $"{key} must be one value, not a list.", $"Write {key}: {describe}.");
            return null;
        }

        if (scalar.Value.Length == 0 || (shape is not null && !shape.IsMatch(scalar.Value)))
        {
            Fail(key, scalar.Value.Length == 0 ? $"{key} is empty." : $"{key} \"{scalar.Value}\" is not {describe}.", $"Write {key}: {describe}.");
        }

        return scalar.Value;
    }

    private string? OneOf(string key, string[] allowed)
    {
        var list = string.Join(", ", allowed);
        var value = Text(key, required: true, shape: null, "one of " + list);
        if (value is not null && !allowed.Contains(value, StringComparer.Ordinal))
        {
            Fail(key, $"{key} \"{value}\" is not known.", "Use one of " + list + ".");
        }

        return value;
    }

    private void CheckGeneratedPair(string? generated, string? sha)
    {
        if (generated is not null && sha is null && !_byKey.ContainsKey("source-sha256"))
        {
            Fail("generated", "generated needs source-sha256.", "Add source-sha256 with the hash of the source.");
        }

        if (sha is not null && generated is null && !_byKey.ContainsKey("generated"))
        {
            Fail("source-sha256", "source-sha256 needs generated.", "Add generated with the tool that wrote the file.");
        }
    }

    private List<string>? List(string key, Func<YamlScalar, bool> ok, string describe)
    {
        if (!_byKey.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.List is not { } items)
        {
            Fail(key, $"{key} must be a list.", $"Write {key}: [...] with {describe}.");
            return [];
        }

        foreach (var item in items.Where(i => !ok(i)))
        {
            Fail(key, $"{key} entry \"{item.Value}\" is not valid.", $"Use {describe}.");
        }

        if (items.Select(static v => v.Value).ToHashSet(StringComparer.Ordinal).Count != items.Count)
        {
            Fail(key, $"{key} lists an entry twice.", "Remove the repeat.");
        }

        return [.. items.Select(static v => v.Value)];
    }

    [GeneratedRegex("^[a-z0-9-]{3,64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DocIdShape { get; }

    [GeneratedRegex("^(?:prj_[0-9A-Za-z]+|linear:project/[^\\s/]+)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProjectShape { get; }

    [GeneratedRegex("^(?:[a-z0-9-]{3,64}|doc:[^/#\\s]+(?:/[^/#\\s]+)?/[a-z0-9-]{3,64})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DocLinkShape { get; }

    [GeneratedRegex("^[0-9a-f]{64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha256Shape { get; }

    [GeneratedRegex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoShape { get; }

    [GeneratedRegex("^[1-9][0-9]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IssueNumberShape { get; }

    [GeneratedRegex("^[A-Z][A-Z0-9]{0,9}-[0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IssueKeyShape { get; }
}
