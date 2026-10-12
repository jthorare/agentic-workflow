using System.Buffers;
using System.Globalization;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// Turns one YAML document into JSON for AwJson (owner decision 2026-10-11: YAML, typed through AwJson). Only the
/// parser and representation model are used, so it stays reflection-free. Scalars follow a subset of the YAML 1.2 core
/// schema (null, booleans, decimal integers and decimals; no exponents, hex, octal or .inf/.nan): plain true/false,
/// null/~, integers and decimals are typed; everything else, and every quoted scalar, is a string ('yes' and 'no' stay
/// strings). Anchors, aliases, tags and duplicate keys are refused: a profile is plain data.
/// </summary>
internal static class YamlToJson
{
    /// <summary>The JSON for <paramref name="yaml"/>, or a message naming the line and column of the problem.</summary>
    /// <param name="yaml">The YAML text.</param>
    /// <param name="json">The UTF-8 JSON, when it converts.</param>
    /// <returns>Null on success, otherwise the problem.</returns>
    internal static string? TryConvert(string yaml, out byte[] json)
    {
        json = [];
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException e)
        {
            return string.Create(CultureInfo.InvariantCulture, $"line {e.Start.Line}, column {e.Start.Column}: {Inner(e).Message}");
        }

        if (stream.Documents.Count != 1)
        {
            return stream.Documents.Count == 0 ? "the file is empty" : "the file holds more than one YAML document";
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            var problem = Write(writer, stream.Documents[0].RootNode, depth: 0);
            if (problem is not null)
            {
                return problem;
            }
        }

        json = buffer.WrittenSpan.ToArray();
        return null;
    }

    private static Exception Inner(Exception e) => e.InnerException is { } inner ? Inner(inner) : e;

    private static string? Write(Utf8JsonWriter writer, YamlNode node, int depth)
    {
        if (!node.Anchor.IsEmpty || !node.Tag.IsEmpty || depth > 32)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"line {node.Start.Line}, column {node.Start.Column}: {(depth > 32 ? "nesting is too deep" : "anchors, aliases and tags aren't allowed in a profile")}");
        }

        return node switch
        {
            YamlMappingNode map => WriteMap(writer, map, depth),
            YamlSequenceNode list => WriteList(writer, list, depth),
            YamlScalarNode scalar => WriteScalar(writer, scalar),
            _ => string.Create(CultureInfo.InvariantCulture, $"line {node.Start.Line}: unsupported YAML node"),
        };
    }

    private static string? WriteMap(Utf8JsonWriter writer, YamlMappingNode map, int depth)
    {
        writer.WriteStartObject();
        foreach (var (key, value) in map.Children)
        {
            if (key is not YamlScalarNode { Value: { } name } || !key.Anchor.IsEmpty || !key.Tag.IsEmpty)
            {
                return string.Create(CultureInfo.InvariantCulture, $"line {key.Start.Line}, column {key.Start.Column}: a key must be plain text");
            }

            writer.WritePropertyName(name);
            if (Write(writer, value, depth + 1) is { } problem)
            {
                return problem;
            }
        }

        writer.WriteEndObject();
        return null;
    }

    private static string? WriteList(Utf8JsonWriter writer, YamlSequenceNode list, int depth)
    {
        writer.WriteStartArray();
        foreach (var item in list.Children)
        {
            if (Write(writer, item, depth + 1) is { } problem)
            {
                return problem;
            }
        }

        writer.WriteEndArray();
        return null;
    }

    private static string? WriteScalar(Utf8JsonWriter writer, YamlScalarNode scalar)
    {
        var text = scalar.Value ?? string.Empty;
        if (scalar.Style is not ScalarStyle.Plain)
        {
            writer.WriteStringValue(text);
            return null;
        }

        switch (text)
        {
            case "" or "~" or "null" or "Null" or "NULL":
                writer.WriteNullValue();
                break;
            case "true" or "True" or "TRUE":
                writer.WriteBooleanValue(value: true);
                break;
            case "false" or "False" or "FALSE":
                writer.WriteBooleanValue(value: false);
                break;
            default:
                WriteNumberOrString(writer, text);
                break;
        }

        return null;
    }

    private static void WriteNumberOrString(Utf8JsonWriter writer, string text)
    {
        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
        {
            writer.WriteNumberValue(whole);
        }
        else if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteStringValue(text);
        }
    }
}
