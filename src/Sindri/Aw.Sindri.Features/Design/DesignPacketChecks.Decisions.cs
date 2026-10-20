using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

internal static partial class DesignPacketChecks
{
    [GeneratedRegex(@"^#{1,6} (?<text>.+?)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Heading { get; }

    /// <summary>DSN-DEC-001: every open decision has 2 to 3 options with trade-offs, exactly one recommended, citations, a non-empty affects, and each affected section carries its assumes marker.</summary>
    internal static void DecisionsWellFormed(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("decisions.json", "sections.json", "spec.md"))
        {
            return;
        }

        var sections = ById(Sections(facts));
        foreach (var decision in Decisions(facts))
        {
            var id = LineJson.Str(decision, "id") ?? string.Empty;
            var recommended = DecisionShape(sink, decision, id);
            if (AssumedOption(decision, recommended) is { } assumed)
            {
                foreach (var affected in LineJson.Strings(decision, "affects"))
                {
                    CheckMarker(facts, sink, (id, affected, assumed), sections);
                }
            }
        }
    }

    private static List<JsonNode> DecisionShape(LineSink sink, JsonNode decision, string id)
    {
        var options = LineJson.Items(decision, "options").ToList();
        if (options.Count is < 2 or > 3)
        {
            sink.Fail(id, $"{id} has {options.Count} options", "give it two or three options");
        }

        var recommended = options.Where(static o => LineJson.Bool(o, "recommended") == true).ToList();
        if (recommended.Count != 1)
        {
            sink.Fail(id, $"{id} has {recommended.Count} recommended options", "mark exactly one option recommended");
        }

        if (options.Exists(static o => string.IsNullOrWhiteSpace(LineJson.Str(o, "tradeoffs"))))
        {
            sink.Fail(id, $"{id} has an option with no trade-offs", "give every option its trade-offs");
        }

        if (!LineJson.Strings(decision, "cites").Any())
        {
            sink.Fail(id, $"{id} cites nothing", "cite the scope ids the question comes from");
        }

        if (!LineJson.Strings(decision, "affects").Any())
        {
            sink.Fail(id, $"{id} affects no section", "list the sections the answer changes");
        }

        return recommended;
    }

    private static void CheckMarker(LineFacts facts, LineSink sink, (string Decision, string Section, string Option) marker, Dictionary<string, JsonNode> sections)
    {
        var (id, affected, assumed) = marker;
        if (!sections.TryGetValue(affected, out var section) || !LineJson.Strings(section, "assumes").Contains(assumed, StringComparer.Ordinal))
        {
            sink.Fail(id, $"{id}: section {affected} does not list assumes {assumed} in sections.json", $"add {assumed} to {affected}'s assumes");
        }
        else if (!SectionBody(facts.Text("spec.md"), LineJson.Str(section, "heading")).Contains($"assumes: {assumed}", StringComparison.Ordinal))
        {
            sink.Fail(id, $"{id}: section {affected} has no 'assumes: {assumed}' line in spec.md", $"mark the section 'assumes: {assumed}'");
        }
    }

    // After the owner's pick the section rests on the chosen option; until then on the recommended one.
    private static string? AssumedOption(JsonNode decision, List<JsonNode> recommended) =>
        string.Equals(LineJson.Str(decision, "status"), "answered", StringComparison.Ordinal) && LineJson.Str(decision, "chosen") is { Length: > 0 } chosen
            ? chosen
            : recommended.Count == 1 ? LineJson.Str(recommended[0], "id") : null;

    /// <summary>The text under a heading of spec.md (any level), up to the next heading; empty when the heading is absent.</summary>
    private static string SectionBody(string spec, string? heading)
    {
        var matches = Heading.Matches(spec);
        for (var i = 0; heading is not null && i < matches.Count; i++)
        {
            if (string.Equals(matches[i].Groups["text"].Value, heading, StringComparison.OrdinalIgnoreCase))
            {
                var start = matches[i].Index + matches[i].Length;
                return spec[start..(i + 1 < matches.Count ? matches[i + 1].Index : spec.Length)];
            }
        }

        return string.Empty;
    }

    /// <summary>DSN-XSPEC-001: every cross-spec edit names a target file and heading that exist at the scope packet's base sha.</summary>
    internal static void EditTargetsExist(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("contracts.json") || !RepoSupplied(facts, sink))
        {
            return;
        }

        foreach (var edit in Edits(facts))
        {
            var id = LineJson.Str(edit, "id") ?? string.Empty;
            var file = LineJson.Str(LineJson.Obj(edit, "target"), "file") ?? string.Empty;
            var heading = LineJson.Str(LineJson.Obj(edit, "target"), "heading") ?? string.Empty;
            if (!facts.Has("repo/" + file))
            {
                sink.Fail(id, $"{id} targets {file}, which does not exist at the base sha", "name a file that exists at the scope packet's base sha");
            }
            else if (!Heading.Matches(facts.Text("repo/" + file)).Any(m => string.Equals(m.Groups["text"].Value, heading, StringComparison.Ordinal)))
            {
                sink.Fail(id, $"{id} targets the heading '{heading}', which {file} does not have", "name a heading the target holds");
            }
        }
    }

    /// <summary>DSN-XSPEC-002: each edit quotes the target's current text, and the quote is a substring of the target at that sha.</summary>
    internal static void EditQuotesExist(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("contracts.json") || !RepoSupplied(facts, sink))
        {
            return;
        }

        foreach (var edit in Edits(facts))
        {
            var id = LineJson.Str(edit, "id") ?? string.Empty;
            var file = LineJson.Str(LineJson.Obj(edit, "target"), "file") ?? string.Empty;
            var quote = LineJson.Str(edit, "quote");
            if (string.IsNullOrWhiteSpace(quote) || (facts.Has("repo/" + file) && !facts.Text("repo/" + file).Contains(quote, StringComparison.Ordinal)))
            {
                sink.Fail(id, $"{id} quotes text that {file} does not hold", "quote the target's current text, word for word");
            }
        }
    }

    /// <summary>DSN-XSPEC-003: each edit is a numbered row of the 'Rows to other lanes' section, one lane per row, and no two edits change one quoted span.</summary>
    internal static void EditsFiledAsRows(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("contracts.json", "spec.md"))
        {
            return;
        }

        var rows = SectionBody(facts.Text("spec.md"), "Rows to other lanes").Split('\n');
        var seen = new List<JsonNode>();
        foreach (var edit in Edits(facts))
        {
            var id = LineJson.Str(edit, "id") ?? string.Empty;
            if (LineJson.Number(edit, "row") is not { } row || !Array.Exists(rows, r => IsRow(r, row, id)))
            {
                sink.Fail(id, $"{id} has no numbered row naming it in 'Rows to other lanes'", "file the edit as one numbered row that ends with its id in parentheses");
            }

            CheckSharing(sink, edit, id, seen);
            seen.Add(edit);
        }
    }

    private static bool IsRow(string line, int row, string id) =>
        line.StartsWith($"{row.ToString(System.Globalization.CultureInfo.InvariantCulture)}. ", StringComparison.Ordinal) && line.Contains($"({id})", StringComparison.Ordinal);

    private static void CheckSharing(LineSink sink, JsonNode edit, string id, List<JsonNode> seen)
    {
        if (seen.Exists(other => string.Equals(LineJson.Str(other, "lane"), LineJson.Str(edit, "lane"), StringComparison.Ordinal)))
        {
            sink.Fail(id, $"{id} shares its lane with an earlier edit", "one lane per row: give each lane its own edit");
        }

        if (seen.Exists(other => Overlaps(other, edit)))
        {
            sink.Fail(id, $"{id} changes a span another edit already changes", "two edits may not change one quoted span; merge them");
        }
    }

    private static bool Overlaps(JsonNode a, JsonNode b) =>
        string.Equals(LineJson.Str(LineJson.Obj(a, "target"), "file"), LineJson.Str(LineJson.Obj(b, "target"), "file"), StringComparison.Ordinal)
        && LineJson.Str(a, "quote") is { Length: > 0 } left && LineJson.Str(b, "quote") is { Length: > 0 } right
        && (left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal));

    private static IEnumerable<JsonNode> Edits(LineFacts facts) => LineJson.Items(facts.Json("contracts.json"), "edits");

    // The repo files come from git at the base sha; a directory with none cannot say whether an edit's target exists.
    private static bool RepoSupplied(LineFacts facts, LineSink sink) =>
        facts.Names.Any(static n => n.StartsWith("repo/", StringComparison.Ordinal)) || sink.Need("repo/");
}
