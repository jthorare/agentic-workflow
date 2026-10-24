using System.Text.RegularExpressions;
using Aw.Judge.Core.Traits;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aw.Judge.Infrastructure.Traits;

/// <summary>
/// Reads one trait file. The format (version 1) follows crosshatch/traits, with the Markdown body moved into 'context',
/// plus 'paths' (scopes a trait to files without a model call) and 'severity' (whether the Stop gate holds the agent
/// back). Unknown keys, a wrong type or an empty text are refused, with the field's name.
/// </summary>
internal static partial class TraitFileParser
{
    internal const string Extension = ".yaml";

    private const int MaxNameLength = 80;

    private static readonly string[] _known = ["version", "name", "subject", "requirement", "applicability", "purpose", "guidance", "context", "paths", "severity"];
    private static readonly string[] _required = ["subject", "requirement", "purpose"];
    private static readonly string[] _optional = ["applicability", "guidance", "context"];
    private static readonly string[] _severities = ["block", "warn"];

    [GeneratedRegex("^(?!trait-)[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NamePattern { get; }

    /// <summary>Parses a trait file.</summary>
    /// <param name="source">The file's text.</param>
    /// <param name="file">The file's path (its name, without the extension, must be the trait's name).</param>
    /// <returns>The trait, or why not in one line.</returns>
    internal static (Trait? Trait, string? Error) Parse(string source, string file)
    {
        if (Mapping(source) is not { } map)
        {
            return (null, MappingProblem(source));
        }

        if (Problem(map) is { } problem)
        {
            return (null, problem);
        }

        var name = Text(map, "name") ?? "";
        var stem = Path.GetFileNameWithoutExtension(file);
        if (!string.Equals(stem, name, StringComparison.Ordinal))
        {
            return (null, $"name \"{name}\" must match the file name \"{stem}{Extension}\"");
        }

        var severity = string.Equals(Text(map, "severity"), "block", StringComparison.Ordinal) ? TraitSeverity.Block : TraitSeverity.Warn;
        return (new Trait(name, Text(map, "subject") ?? "", Text(map, "requirement") ?? "", Text(map, "purpose") ?? "", severity, file, Text(map, "applicability"), Text(map, "guidance"), Text(map, "context"), Paths(map)), null);
    }

    private static YamlMappingNode? Mapping(string source)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(source));
            return stream.Documents.Count > 0 ? stream.Documents[0].RootNode as YamlMappingNode : null;
        }
        catch (YamlException)
        {
            return null;
        }
    }

    // Why a file is no mapping: it isn't YAML, or its top level isn't a mapping.
    private static string MappingProblem(string source)
    {
        try
        {
            new YamlStream().Load(new StringReader(source));
            return "trait: expected a mapping";
        }
        catch (YamlException e)
        {
            return $"invalid YAML: {e.Message.Split('\n')[0]}";
        }
    }

    // The first thing wrong with the mapping, in the order of the TS schema: unknown keys, version, name, the texts, paths, severity.
    private static string? Problem(YamlMappingNode map) =>
        UnknownKey(map)
        ?? (string.Equals(Scalar(map, "version"), "1", StringComparison.Ordinal) ? null : "version: must be 1")
        ?? NameProblem(Scalar(map, "name"))
        ?? TextProblem(map)
        ?? (map.Children.ContainsKey("paths") ? PathsProblem(map) : null)
        ?? (Scalar(map, "severity") is { } severity && _severities.Contains(severity, StringComparer.Ordinal) ? null : "severity: must be block or warn");

    private static string? UnknownKey(YamlMappingNode map)
    {
        var unknown = map.Children.Keys.FirstOrDefault(key => key is not YamlScalarNode { Value: { } text } || !_known.Contains(text, StringComparer.Ordinal));
        return unknown is null ? null : $"trait: unrecognized key {(unknown as YamlScalarNode)?.Value ?? "(not a name)"}";
    }

    private static string? NameProblem(string? name) => name is { Length: <= MaxNameLength } && NamePattern.IsMatch(name) ? null : "name: invalid";

    private static string? TextProblem(YamlMappingNode map) =>
        _required.FirstOrDefault(key => Text(map, key) is null) is { } missing ? $"{missing}: required text"
        : _optional.FirstOrDefault(key => map.Children.ContainsKey(key) && Text(map, key) is null) is { } wrong ? $"{wrong}: must be text"
        : null;

    private static string? PathsProblem(YamlMappingNode map)
    {
        if (map.Children["paths"] is not YamlSequenceNode { Children.Count: > 0 } paths)
        {
            return "paths: must be a non-empty list";
        }

        var globs = paths.Children.Select(node => (node as YamlScalarNode)?.Value).ToList();
        return globs.Exists(string.IsNullOrEmpty) ? "paths: each must be text"
            : globs.Exists(glob => TraitGlob.HasUnclosedBrace(glob ?? "")) ? "paths: unclosed { in glob"
            : null;
    }

    private static List<string>? Paths(YamlMappingNode map) =>
        map.Children.TryGetValue("paths", out var node) && node is YamlSequenceNode sequence
            ? [.. sequence.Children.OfType<YamlScalarNode>().Select(scalar => scalar.Value).OfType<string>()]
            : null;

    private static string? Scalar(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(key, out var node) && node is YamlScalarNode scalar ? scalar.Value : null;

    // Text with something besides whitespace.
    private static string? Text(YamlMappingNode map, string key) => Scalar(map, key) is { } text && text.Any(c => !char.IsWhiteSpace(c)) ? text : null;
}
