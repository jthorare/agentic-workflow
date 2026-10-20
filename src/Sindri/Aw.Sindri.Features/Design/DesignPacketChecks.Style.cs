using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

internal static partial class DesignPacketChecks
{
    [GeneratedRegex(@"^## (?<text>.+?)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TopHeading { get; }

    /// <summary>DSN-STYLE-001: spec.md follows the style pack's section rules: required sections in order, each within its word limit, the forbidden phrases absent, within the line cap (line spec §5.1).</summary>
    internal static void FollowsStylePack(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("spec.md", "pack/style.json"))
        {
            return;
        }

        var pack = facts.Json("pack/style.json");
        var spec = facts.Text("spec.md");
        var heads = TopHeading.Matches(spec);
        var wanted = LineJson.Items(pack, "sections").ToList();
        var known = wanted.Select(static s => LineJson.Str(s, "heading")).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var head in heads.Where(h => !known.Contains(h.Groups["text"].Value)))
        {
            sink.Fail(head.Groups["text"].Value, $"unknown section '{head.Groups["text"].Value}'", "use only the style pack's sections");
        }

        CheckSections(sink, spec, heads, wanted);
        CheckRules(sink, spec, LineJson.Obj(pack, "rules"));
    }

    private static void CheckSections(LineSink sink, string spec, MatchCollection heads, List<JsonNode> wanted)
    {
        var last = -1;
        foreach (var section in wanted)
        {
            var heading = LineJson.Str(section, "heading") ?? string.Empty;
            var index = IndexOf(heads, heading);
            if (index < 0)
            {
                if (LineJson.Bool(section, "required") == true)
                {
                    sink.Fail(heading, $"missing required section '{heading}'", $"add a '## {heading}' section");
                }

                continue;
            }

            if (index < last)
            {
                sink.Fail(heading, $"section '{heading}' is out of order", "put the sections in the style pack's order");
            }

            last = Math.Max(last, index);
            if (LineJson.Number(section, "maxWords") is { } max && Words(Body(spec, heads, index)) is var words && words > max)
            {
                sink.Fail(heading, $"section '{heading}' has {words.ToString(CultureInfo.InvariantCulture)} words, over {max.ToString(CultureInfo.InvariantCulture)}", "shorten the section");
            }
        }
    }

    private static void CheckRules(LineSink sink, string spec, JsonNode? rules)
    {
        foreach (var phrase in LineJson.Strings(rules, "forbidden"))
        {
            if (Regex.IsMatch(spec, $@"(^|[^\p{{L}}\p{{N}}'])({Regex.Escape(phrase)})($|[^\p{{L}}\p{{N}}'])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                sink.Fail(item: null, $"forbidden phrase '{phrase}'", "reword it");
            }
        }

        var lines = spec.Split('\n').Length - (spec.EndsWith('\n') ? 1 : 0);
        if (LineJson.Number(rules, "maxLines") is { } cap && lines > cap)
        {
            sink.Fail(item: null, $"{lines.ToString(CultureInfo.InvariantCulture)} lines, over {cap.ToString(CultureInfo.InvariantCulture)}", "cut the spec to the pack's line cap");
        }
    }

    private static int IndexOf(MatchCollection heads, string heading)
    {
        for (var i = 0; i < heads.Count; i++)
        {
            if (string.Equals(heads[i].Groups["text"].Value, heading, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Body(string spec, MatchCollection heads, int index)
    {
        var start = heads[index].Index + heads[index].Length;
        var end = index + 1 < heads.Count ? heads[index + 1].Index : spec.Length;
        return spec[start..end];
    }

    private static int Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
