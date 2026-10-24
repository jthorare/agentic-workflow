using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Extract;

/// <summary>The forms the extractor recognizes (S-4), each pure over one file's text. Only source files count: not tests, not vendored or built output. A form is one row of <see cref="All"/>: adding one is adding a row.</summary>
public static partial class Recognizers
{
    internal const int MaxFileLength = 512_000;
    private static readonly string[] _extensions = [".ts", ".tsx", ".js", ".jsx", ".mjs", ".json", ".cs"];
    private static readonly HashSet<string> _localeFolders = new(["i18n", "l10n", "locale", "locales", "lang", "translations"], StringComparer.OrdinalIgnoreCase);
    private static readonly string[] _notComponents = ["Context", "Schema", "Props"];
    private static readonly string[] _skipped = ["node_modules/", "dist/", "build/", "coverage/", ".git/", "tests/", "__tests__/"];

    /// <summary>Every recognizer, in the order their findings are reported.</summary>
    public static IReadOnlyList<Recognizer> All { get; } =
    [
        new("command-ids", Script, CommandIds),
        new("command-registry", Script, Registry),
        new("keybindings", Script, Keybindings),
        new("settings", f => Source(f) && SettingsFile.IsMatch(f.Path) && !InToolFolder(f.Path) && !NotTheAppsSettings(f.Path), SettingKeys),
        new("routes", Script, Routes),
        new("minimal-api-routes", f => Source(f) && IsCSharp(f.Path), MinimalApiRoutes),
        new("components", f => Source(f) && IsComponentFile(f.Path), Components),
        new("test-ids", Script, TestIds),
    ];

    /// <summary>Whether the extractor reads a file at all.</summary>
    /// <param name="file">The file.</param>
    /// <returns>True for a small source file outside tests and build output.</returns>
    public static bool Source(RepoFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.Text.Length <= MaxFileLength && MayRead(file.Path);
    }

    /// <summary>Whether a path is a glossary file the extractor reads for domain concepts (CONTEXT.md or GLOSSARY.md, any directory).</summary>
    /// <param name="path">The path from the repo root.</param>
    /// <returns>True for a glossary file.</returns>
    public static bool IsGlossaryFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Glossary.IsGlossaryFile(path);
    }

    /// <summary>Whether a path could be a source file the extractor reads, before its text is fetched.</summary>
    /// <param name="path">The path from the repo root.</param>
    /// <returns>True for a path with a source extension outside tests and build output.</returns>
    public static bool MayRead(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var rooted = "/" + path;
        return _extensions.Any(e => path.EndsWith(e, StringComparison.Ordinal))
            && !_skipped.Any(s => rooted.Contains("/" + s, StringComparison.Ordinal))
            && !path.Contains(".test.", StringComparison.Ordinal)
            && !path.Contains(".spec.", StringComparison.Ordinal);
    }

    // The TypeScript and JavaScript forms: C# named arguments (id: "a", title: "b") would read as a command registry entry.
    private static bool Script(RepoFile file) => Source(file) && !IsCSharp(file.Path);

    private static bool IsCSharp(string path) => path.EndsWith(".cs", StringComparison.Ordinal);

    // The app's own settings registry is a tracked file of its source; a dot-folder (.vscode, .devcontainer, .idea) holds a tool's settings, not the product's.
    private static bool InToolFolder(string path) => path.Split('/').SkipLast(1).Any(segment => segment.StartsWith('.'));

    // .NET's appsettings*.json, JSON Schemas and locale files (src/i18n/en/settings.json) are named like a settings registry but are not the product's settings.
    private static bool NotTheAppsSettings(string path)
    {
        var segments = path.Split('/');
        var name = segments[^1];
        return name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase)
            || segments.SkipLast(1).Any(_localeFolders.Contains);
    }

    private static bool IsComponentFile(string path) => path.EndsWith(".tsx", StringComparison.Ordinal) || path.EndsWith(".jsx", StringComparison.Ordinal);

    private static IEnumerable<Found> CommandIds(RepoFile file) =>
        CommandUnion.Matches(file.Text).SelectMany(m => Literal.Matches(m.Groups["body"].Value))
            .Select(m => Command(m.Groups["v"].Value, Humanize(m.Groups["v"].Value), named: false)).OfType<Found>();

    private static IEnumerable<Found> Registry(RepoFile file) =>
        RegistryEntry.Matches(file.Text).Select(m => Command(m.Groups["id"].Value, m.Groups["title"].Value, named: true)).OfType<Found>();

    private static IEnumerable<Found> Keybindings(RepoFile file) =>
        KeyTable.Matches(file.Text).SelectMany(t => Binding.Matches(t.Groups["body"].Value)).Select(m =>
        {
            var combo = Make("surface:keybinding/" + m.Groups["combo"].Value);
            var command = Make("surface:command/" + m.Groups["id"].Value);
            GraphEdge[] edges = command is { } to && combo is { } from ? [new GraphEdge(from, to, EdgeType.MovesTo, Provenance.Deterministic)] : [];
            return combo is { } key ? new Found(key, m.Groups["combo"].Value, 1.0, Named: true, edges) : null;
        }).OfType<Found>();

    private static IEnumerable<Found> SettingKeys(RepoFile file)
    {
        try
        {
            using var document = JsonDocument.Parse(file.Text);
            return document.RootElement.ValueKind != JsonValueKind.Object
                ? []
                : [.. document.RootElement.EnumerateObject().Select(p => Named("surface:setting/" + p.Name, p.Name, 1.0)).OfType<Found>()];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<Found> Routes(RepoFile file) =>
        Route.Matches(file.Text).Select(m => (Method: m.Groups["m"].Value.ToUpperInvariant(), Path: m.Groups["p"].Value))
            .Select(r => Named($"surface:route/{r.Method}:{r.Path}", $"{r.Method} {r.Path}", 1.0)).OfType<Found>();

    private static IEnumerable<Found> MinimalApiRoutes(RepoFile file) =>
        MinimalApiRoute.Matches(file.Text).Select(m => (Method: m.Groups["m"].Value.ToUpperInvariant(), Path: m.Groups["p"].Value))
            .Select(r => Named($"surface:route/{r.Method}:{r.Path}", $"{r.Method} {r.Path}", 1.0)).OfType<Found>();

    private static IEnumerable<Found> Components(RepoFile file) =>
        Component.Matches(file.Text).Select(m => m.Groups["n"].Value).Where(n => !_notComponents.Any(suffix => n.EndsWith(suffix, StringComparison.Ordinal))).Select(n => Named($"surface:{ComponentKind(n)}/{n}", n, 0.9)).OfType<Found>();

    private static IEnumerable<Found> TestIds(RepoFile file) =>
        TestId.Matches(file.Text).Select(m => m.Groups["id"].Value).Select(id => Named("surface:pane/" + id, id, 0.8)).OfType<Found>();

    private static string ComponentKind(string name) =>
        name.EndsWith("Dialog", StringComparison.Ordinal) || name.EndsWith("Modal", StringComparison.Ordinal) ? "dialog"
        : name.EndsWith("Screen", StringComparison.Ordinal) || name.EndsWith("Page", StringComparison.Ordinal) ? "screen"
        : "pane";

    private static Found? Command(string id, string label, bool named) => Make("surface:command/" + id) is { } key ? new Found(key, label, 1.0, named, []) : null;

    private static Found? Named(string key, string label, double confidence) => Make(key) is { } k ? new Found(k, label, confidence, Named: true, []) : null;

    private static NodeKey? Make(string value) => NodeKey.TryFrom(value, out var key) && NodeKinds.HasValidSurfaceKind(key) ? key : null;

    private static string Humanize(string id)
    {
        var words = id.Replace('-', ' ').Replace('_', ' ');
        return words.Length == 0 ? id : string.Concat(words[..1].ToUpper(CultureInfo.InvariantCulture), words.AsSpan(1));
    }

    [GeneratedRegex("(?:^|/)[^/]*settings[^/]*\\.json\\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SettingsFile { get; }

    [GeneratedRegex("type\\s+\\w*Command(?:Id|Name)\\s*=\\s*(?<body>(?:\\s*\\|?\\s*\"[^\"\\r\\n]+\")+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommandUnion { get; }

    [GeneratedRegex("\"(?<v>[^\"\\r\\n]+)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Literal { get; }

    [GeneratedRegex("\\bid:\\s*\"(?<id>[A-Za-z0-9][\\w.-]*)\"\\s*,\\s*title:\\s*\"(?<title>[^\"\\r\\n]*)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RegistryEntry { get; }

    [GeneratedRegex("Record<\\s*string\\s*,\\s*\\w*Command(?:Id|Name)\\s*>\\s*=\\s*\\{(?<body>[^}]*)\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyTable { get; }

    [GeneratedRegex("\"(?<combo>[^\"\\r\\n]+)\"\\s*:\\s*\"(?<id>[^\"\\r\\n]+)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Binding { get; }

    [GeneratedRegex("\\b(?:app|router)\\.(?<m>get|post|put|patch|delete)\\(\\s*[\"'`](?<p>/[^\"'`\\s]*)[\"'`]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Route { get; }

    // ASP.NET Core minimal APIs: a Map{Get,Post,Put,Patch,Delete} call on an app or a group whose first argument is a literal path from '/'. The path is the one written there, without any MapGroup prefix (that is often set in another file).
    [GeneratedRegex("\\.Map(?<m>Get|Post|Put|Patch|Delete)\\(\\s*\"(?<p>/[^\"\\s]*)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MinimalApiRoute { get; }

    [GeneratedRegex("\\bexport\\s+(?:default\\s+)?(?:async\\s+)?(?:function|const|class)\\s+(?<n>[A-Z][A-Za-z0-9]*[a-z][A-Za-z0-9]*)(?![A-Za-z0-9_])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Component { get; }

    [GeneratedRegex("data-testid=[\"'](?<id>[A-Za-z0-9][\\w.-]*)[\"']", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TestId { get; }
}
