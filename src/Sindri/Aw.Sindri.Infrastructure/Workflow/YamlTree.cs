using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Validation;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Reads one YAML document from YamlDotNet's parser events into a JSON tree (spec §3.1), recording every node's
/// position (a mapping entry where its key is written, a list item where it starts) and refusing, as RCP-SCHEMA-003
/// at each place: duplicate keys, anchors, aliases, merge keys, explicit tags, non-text keys and nesting deeper than
/// 32. It keeps going after each refusal, so every one is reported; only a syntax error stops it.
/// </summary>
internal static class YamlTree
{
    internal const int MaxDepth = 32;
    private const string Rule = "RCP-SCHEMA-003";

    /// <summary>The document's root, or null when the file has none or isn't YAML (a finding says why).</summary>
    public static JsonNode? Read(string text, ReaderFindings findings)
    {
        var parser = new Parser(new StringReader(text));
        try
        {
            _ = parser.Consume<StreamStart>();
            if (parser.Accept<StreamEnd>(out _))
            {
                findings.At(Rule, string.Empty, SourcePosition.Start, "the file holds no YAML document", "write the recipe as one YAML mapping");
                return null;
            }

            _ = parser.Consume<DocumentStart>();
            findings.Record(string.Empty, Position(parser.Current!.Start));   // why: Consume moved to the root node's event
            var root = Node(parser, string.Empty, depth: 1, findings);
            _ = parser.Consume<DocumentEnd>();
            if (!parser.Accept<StreamEnd>(out _))
            {
                findings.At(Rule, string.Empty, Position(parser.Current!.Start), "the file holds more than one YAML document", "keep one recipe per file");   // why: Accept failed, so there is a current event
            }

            return root;
        }
        catch (YamlException e)
        {
            findings.At(Rule, string.Empty, Position(e.Start), $"the file isn't valid YAML: {Innermost(e).Message}", "fix the YAML at that position");
            return null;
        }
        catch (InvalidOperationException e) when (e.Source == typeof(Parser).Assembly.GetName().Name)   // why: YamlDotNet's scanner throws this, not a YamlException, on some unclosed flow collections; a bug of ours still reaches the CLI's boundary
        {
            var at = parser.Current is { } current ? Position(current.Start) : SourcePosition.Start;
            findings.At(Rule, string.Empty, at, "the file isn't valid YAML: the parser stopped here, often at a '[' or '{' that isn't closed", "fix the YAML at that position");
            return null;
        }
    }

    internal static SourcePosition Position(Mark mark) => new((int)mark.Line, (int)mark.Column);

    private static Exception Innermost(Exception e) => e.InnerException is { } inner ? Innermost(inner) : e;

    private static JsonNode? Node(IParser parser, string path, int depth, ReaderFindings findings)
    {
        var current = parser.Current!;   // why: callers stop at the end events, so a node event is current
        if (current is AnchorAlias alias)
        {
            findings.At(Rule, path, Position(alias.Start), $"alias '*{alias.Value.Value}' isn't allowed", "write the value out in full; recipes don't use anchors or aliases");
            findings.Refuse(path);
            _ = parser.MoveNext();
            return null;
        }

        if (current is NodeEvent node)
        {
            Properties(node, path, findings);
        }

        if (current is Scalar scalar)
        {
            _ = parser.MoveNext();
            return YamlScalars.Resolve(scalar, path, findings);
        }

        if (depth > MaxDepth)
        {
            findings.At(Rule, path, Position(current.Start),
                string.Create(CultureInfo.InvariantCulture, $"the recipe nests deeper than {MaxDepth} levels"), "flatten the structure");
            findings.Refuse(path);
            Skip(parser);
            return null;
        }

        return current is SequenceStart ? Sequence(parser, path, depth, findings) : Mapping(parser, path, depth, findings);
    }

    private static void Properties(NodeEvent node, string path, ReaderFindings findings)
    {
        if (!node.Anchor.IsEmpty)
        {
            findings.At(Rule, path, Position(node.Start), $"anchor '&{node.Anchor.Value}' isn't allowed", "write the value out in full; recipes don't use anchors or aliases");
        }

        if (!node.Tag.IsEmpty)
        {
            findings.At(Rule, path, Position(node.Start), $"explicit tag '{node.Tag.Value}' isn't allowed", "remove the tag; values are read with the YAML 1.2 core schema");
        }
    }

    private static JsonArray Sequence(IParser parser, string path, int depth, ReaderFindings findings)
    {
        var array = new JsonArray();
        _ = parser.MoveNext();
        while (parser.Current is not SequenceEnd)
        {
            var item = string.Create(CultureInfo.InvariantCulture, $"{path}[{array.Count}]");
            findings.Record(item, Position(parser.Current!.Start));   // why: the loop stops at SequenceEnd, so an item event is current
            array.Add(Node(parser, item, depth + 1, findings));
        }

        _ = parser.MoveNext();
        return array;
    }

    private static JsonObject Mapping(IParser parser, string path, int depth, ReaderFindings findings)
    {
        var map = new JsonObject();
        _ = parser.MoveNext();
        while (parser.Current is not MappingEnd)
        {
            var at = Position(parser.Current!.Start);   // why: the loop stops at MappingEnd, so a key event is current
            var key = Key(parser, path, findings);
            if (key is null)
            {
                Skip(parser);
                continue;
            }

            var entry = ReaderFindings.Join(path, key);
            if (map.ContainsKey(key))
            {
                findings.At(Rule, entry, at, $"key '{key}' is written twice", $"keep one '{key}'");
                Skip(parser);
                continue;
            }

            findings.Record(entry, at);
            map[key] = Node(parser, entry, depth + 1, findings);
        }

        _ = parser.MoveNext();
        return map;
    }

    /// <summary>Consumes a key; null when it can't be one (a finding says why), and the caller skips its value.</summary>
    private static string? Key(IParser parser, string path, ReaderFindings findings)
    {
        if (parser.Current is not Scalar key)
        {
            findings.At(Rule, path, Position(parser.Current!.Start), "a key must be plain text", "write the key as text");   // why: called with a key event current
            Skip(parser);
            return null;
        }

        Properties(key, ReaderFindings.Join(path, key.Value), findings);
        _ = parser.MoveNext();
        if (key.Style == ScalarStyle.Plain && string.Equals(key.Value, "<<", StringComparison.Ordinal))
        {
            findings.At(Rule, ReaderFindings.Join(path, "<<"), Position(key.Start), "merge key '<<' isn't allowed", "write the merged fields out in full");
            return null;
        }

        return key.Value;
    }

    /// <summary>Skips the node whose event is current, with everything inside it.</summary>
    private static void Skip(IParser parser)
    {
        var level = 0;
        do
        {
            level += parser.Current!.NestingIncrease;   // why: a node event is current until the matching end is passed
            _ = parser.MoveNext();
        }
        while (level > 0);
    }
}
