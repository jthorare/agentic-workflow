using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Extract;

/// <summary>
/// Finds the pages and dialogs among a repo's Avalonia views (pages spec §3.2): an '.axaml' file whose root 'x:Class' (else its file name) ends in Page or Screen is
/// 'surface:screen/&lt;Class&gt;', in Dialog or Modal 'surface:dialog/&lt;Class&gt;'. Any other view (a window, a control, styles, a resource dictionary) makes nothing.
/// The page is 'implements'-linked from the view, its code-behind ('&lt;File&gt;.axaml.cs') and the view model named by 'x:DataType', found by class name among the repo's '.cs' files.
/// Pure and ordered.
/// </summary>
internal static partial class AxamlPass
{
    private const double Confidence = 0.9;
    private const string Extension = ".axaml";

    /// <summary>Whether a path is an Avalonia view.</summary>
    /// <param name="path">The path from the repo root.</param>
    /// <returns>True for an '.axaml' file.</returns>
    internal static bool IsView(string path) => path.EndsWith(Extension, StringComparison.Ordinal);

    /// <summary>Derives the page surfaces from the views.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="files">The files at the commit.</param>
    /// <returns>The surfaces, their code files and the 'implements' edges.</returns>
    internal static ExtractedMap Run(ContextRepoName repo, IReadOnlyList<RepoFile> files)
    {
        var readable = files.Where(Recognizers.Source).OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        var views = readable.Where(f => IsView(f.Path)).ToList();
        if (views.Count == 0)
        {
            return ExtractedMap.Empty;
        }

        var byPath = readable.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var declared = new Lazy<Dictionary<string, string>>(() => Declarations(readable));
        List<GraphNode> nodes = [];
        List<GraphEdge> edges = [];
        foreach (var view in views)
        {
            var root = Root(view.Text);
            var name = ClassName(root) ?? view.Path[(view.Path.LastIndexOf('/') + 1)..^Extension.Length];
            var kind = Recognizers.ComponentKind(name);
            if (kind == "pane" || !NodeKey.TryFrom($"surface:{kind}/{name}", out var surface) || !NodeKinds.HasValidSurfaceKind(surface))
            {
                continue;
            }

            var paths = Homes(view.Path, DataTypeName(root), byPath, declared);
            var evidence = paths.Select(p => MapExtractor.Evidence(repo, p)).OfType<EvidenceRef>().ToList();
            nodes.Add(new GraphNode(surface, name, Provenance.Deterministic, Confidence, evidence));
            foreach (var path in paths.Where(p => MapExtractor.CodeKey(p) is not null && MapExtractor.Evidence(repo, p) is not null))
            {
                nodes.Add(MapExtractor.CodeNode(path));
                edges.Add(new GraphEdge(MapExtractor.CodeKey(path)!.Value, surface, EdgeType.Implements, Provenance.Deterministic, 1.0, [MapExtractor.Evidence(repo, path)!]));   // why: the filter above keeps valid keys
            }
        }

        return ExtractedMap.Empty.Plus(new ExtractedMap(nodes, edges));
    }

    // The view, its code-behind when present, and the file declaring its view model when it resolves.
    private static List<string> Homes(string view, string? model, HashSet<string> paths, Lazy<Dictionary<string, string>> declared)
    {
        List<string> homes = [view];
        if (paths.Contains(view + ".cs"))
        {
            homes.Add(view + ".cs");
        }

        if (model is not null && declared.Value.TryGetValue(model, out var modelPath) && !homes.Contains(modelPath, StringComparer.Ordinal))
        {
            homes.Add(modelPath);
        }

        return homes;
    }

    // The first element's start tag, past the XML declaration, comments and doctype.
    private static string Root(string text) => RootTag.Match(text) is { Success: true } m ? m.Value : "";

    private static string? ClassName(string root) => Attribute(root, "x:Class") is { } full ? full[(full.LastIndexOf('.') + 1)..] : null;

    // 'vm:SettingsViewModel' and '{x:Type vm:SettingsViewModel}' both give 'SettingsViewModel'.
    private static string? DataTypeName(string root) =>
        Attribute(root, "x:DataType") is { } type ? type.Trim('{', '}').Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].Split(':')[^1].Split('.')[^1] : null;

    private static string? Attribute(string root, string name)
    {
        var match = Regex.Match(root, $"\\s{Regex.Escape(name)}\\s*=\\s*\"(?<v>[^\"]+)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups["v"].Value : null;
    }

    // Class, record and struct names to the first '.cs' file (by path order) that declares them.
    private static Dictionary<string, string> Declarations(List<RepoFile> readable)
    {
        Dictionary<string, string> declared = new(StringComparer.Ordinal);
        foreach (var file in readable.Where(f => Recognizers.IsCSharp(f.Path)))
        {
            foreach (Match m in Declaration.Matches(file.Text))
            {
                _ = declared.TryAdd(m.Groups["n"].Value, file.Path);
            }
        }

        return declared;
    }

    [GeneratedRegex("<(?![?!])[\\w:.]+(?:\\s[^>]*)?>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RootTag { get; }

    [GeneratedRegex("\\b(?:class|record|struct)\\s+(?<n>[A-Za-z_]\\w*)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Declaration { get; }
}
