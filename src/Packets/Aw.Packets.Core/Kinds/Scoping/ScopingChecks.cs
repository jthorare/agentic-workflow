using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel.Json;
using Aw.Packets.Core.Checks;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>
/// The scoping kind's schema checks (PKT-SCP-001..006, <see cref="ScopingRules"/>). Pure: the directory's files come in.
/// What the files mean, and every check across files (SCP-LENS-001 ties lenses.json to coverage.json), stays in Sindri.
/// </summary>
public static partial class ScopingChecks
{
    /// <summary>The highest depth a lens entry may record.</summary>
    public const int MaxReach = 32;

    private const string LensesFile = "lenses.json";
    private const string GraphFile = "graph.json";

    /// <summary>Runs every scoping rule in <see cref="ScopingRules"/> order.</summary>
    /// <param name="files">The packet directory as read.</param>
    /// <param name="json">The package's JSON options, to read lenses.json.</param>
    /// <returns>One record per failure, or one pass per rule; a rule that can't run is unchecked.</returns>
    public static IReadOnlyList<CheckResult> Run(ScopingFiles files, JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(json);
        var checks = new CheckList();
        var kind = PacketKinds.Scoping;
        checks.Rule(
            ScopingRules.FilesPresent,
            $"all {kind.Files.Count} scoping files are present",
            kind.Files.Where(f => !files.Present.ContainsKey(f.Name)).Select(f => (
                $"file {f.Name}",
                $"{f.Name} is missing.",
                $"Write {f.Name} into the packet directory.")));
        checks.Rule(
            ScopingRules.NoUnlisted,
            "the directory holds only the scoping files",
            files.Unlisted.Select(static name => (
                $"entry {name}",
                $"{name} is not a file of the scoping kind.",
                $"Remove {name} from the packet directory.")));
        var malformed = WellFormed(files).ToList();
        checks.Rule(ScopingRules.WellFormed, "every file is well formed", malformed.Select(static m => (m.Subject, m.Message, m.Fix)));
        var bad = malformed.Select(static m => m.Name).ToHashSet(StringComparer.Ordinal);
        LensChecks(files, json, bad, checks);
        GraphChecks(files, bad, checks);
        return checks.Results;
    }

    private static void GraphChecks(ScopingFiles files, HashSet<string> malformed, CheckList checks)
    {
        if (!files.Present.TryGetValue(GraphFile, out var bytes) || malformed.Contains(GraphFile))
        {
            checks.Unchecked(ScopingRules.GraphShape, "graph.json is missing or not well formed, so its shape can't be checked");
            return;
        }

        checks.Rule(ScopingRules.GraphShape, "graph.json has unique node ids and whole edges", GraphFailures(bytes).Select(static f => ($"graph.json {f.Where}", f.Message, f.Fix)));
    }

    private static IEnumerable<(string Where, string Message, string Fix)> GraphFailures(ReadOnlyMemory<byte> bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (!HasSchemaVersion(root))
        {
            yield return ("schemaVersion", $"schemaVersion is not {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads version {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Write schemaVersion 1.");
        }

        foreach (var failure in NodeFailures(Items(root, "nodes")))
        {
            yield return failure;
        }

        foreach (var failure in EdgeFailures(Items(root, "edges")))
        {
            yield return failure;
        }
    }

    private static bool HasSchemaVersion(JsonElement root) =>
        root.TryGetProperty("schemaVersion", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) && number == PacketReader.SchemaVersion;

    private static IEnumerable<(string Where, string Message, string Fix)> NodeFailures(List<JsonElement> nodes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < nodes.Count; index++)
        {
            var where = Position("nodes", index);
            var id = nodes[index].ValueKind == JsonValueKind.Object && nodes[index].TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (string.IsNullOrEmpty(id))
            {
                yield return (where, $"{where} has no id.", "Give every node its map key as id.");
            }
            else if (!seen.Add(id))
            {
                yield return (where, $"{where} '{id}' is used more than once.", "Give every node its own id.");
            }
        }
    }

    private static IEnumerable<(string Where, string Message, string Fix)> EdgeFailures(List<JsonElement> edges)
    {
        for (var index = 0; index < edges.Count; index++)
        {
            if (!HasEnd(edges[index], "from") || !HasEnd(edges[index], "to"))
            {
                var where = Position("edges", index);
                yield return (where, $"{where} has an empty end.", "Give both ends of the edge.");
            }
        }
    }

    private static string Position(string list, int index) => $"{list}[{index.ToString(CultureInfo.InvariantCulture)}]";

    private static List<JsonElement> Items(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array ? [.. array.EnumerateArray()] : [];

    private static bool HasEnd(JsonElement edge, string name) =>
        edge.ValueKind == JsonValueKind.Object && edge.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 };

    private static IEnumerable<(string Name, string Subject, string Message, string Fix)> WellFormed(ScopingFiles files)
    {
        foreach (var spec in PacketKinds.Scoping.Files)
        {
            if (files.Present.TryGetValue(spec.Name, out var bytes) && PacketFileShape.Problem(spec, bytes) is { } message)
            {
                yield return (spec.Name, $"file {spec.Name}", message, PacketFileShape.Fix(spec));
            }
        }
    }

    private static void LensChecks(ScopingFiles files, JsonSerializerOptions json, HashSet<string> malformed, CheckList checks)
    {
        if (!files.Present.TryGetValue(LensesFile, out var bytes) || malformed.Contains(LensesFile))
        {
            const string Why = "lenses.json is missing or not well formed, so its schema can't be checked";
            checks.Unchecked(ScopingRules.LensesSchema, Why);
            checks.Unchecked(ScopingRules.LensIds, Why);
            return;
        }

        var parsed = AwJson.Deserialize<LensesFile>(bytes.Span, json);
        if (parsed.IsError)
        {
            checks.Rule(ScopingRules.LensesSchema, string.Empty, [("file lenses.json", parsed.Error.Message, "Fix lenses.json to match schemas/packets/lenses.schema.json.")]);
            checks.Unchecked(ScopingRules.LensIds, "lenses.json doesn't match its schema, so its ids can't be checked");
            return;
        }

        var lenses = Entries(parsed.Value);
        checks.Rule(
            ScopingRules.LensesSchema,
            "lenses.json matches its schema",
            SchemaFailures(parsed.Value, lenses).Select(static f => ($"lenses.json {f.Where}", f.Message, f.Fix)));
        checks.Rule(
            ScopingRules.LensIds,
            "every lens item id is unique and names its own lens",
            IdFailures(lenses).Select(static f => ($"lenses.json {f.Where}", f.Message, f.Fix)));
    }

    private static List<(string Name, LensEntry Entry)> Entries(LensesFile file) =>
        [("code", file.Code), ("product", file.Product), ("related", file.Related), ("history", file.History)];

    private static IEnumerable<(string Where, string Message, string Fix)> SchemaFailures(LensesFile file, List<(string Name, LensEntry Entry)> lenses)
    {
        if (file.SchemaVersion != PacketReader.SchemaVersion)
        {
            yield return ("schemaVersion", $"schemaVersion is {file.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
        }

        foreach (var (name, entry) in lenses)
        {
            foreach (var failure in EntryFailures(name, entry))
            {
                yield return failure;
            }
        }
    }

    private static IEnumerable<(string Where, string Message, string Fix)> EntryFailures(string lens, LensEntry entry)
    {
        foreach (var (field, value) in new (string, int)[] { ("depth", entry.Depth) })
        {
            if (value is < 0 or > MaxReach)
            {
                yield return ($"{lens}.{field}", $"{lens}.{field} is {value.ToString(CultureInfo.InvariantCulture)}; it is 0 to {MaxReach.ToString(CultureInfo.InvariantCulture)}.", $"Record the {field} the lens ran with.");
            }
        }

        foreach (var failure in CountFailures(lens, entry))
        {
            yield return failure;
        }

        foreach (var (id, where) in Ids(lens, entry).Where(static i => !IdShape.IsMatch(i.Id)))
        {
            yield return (where, $"{where} '{id}' is not an id of the form L-<lens>-<n>.", "Use L-code-1, L-product-1, L-related-1 or L-history-1, counting from 1.");
        }

        for (var index = 0; index < entry.Edges.Count; index++)
        {
            if (entry.Edges[index].From.Length == 0 || entry.Edges[index].To.Length == 0)
            {
                var where = $"{lens}.edges[{index.ToString(CultureInfo.InvariantCulture)}]";
                yield return (where, $"{where} has an empty end.", "Give both ends of the edge.");
            }
        }
    }

    private static IEnumerable<(string Where, string Message, string Fix)> CountFailures(string lens, LensEntry entry)
    {
        if (entry.Hops.Distinct().Take(entry.Hops.Count).Count() != entry.Hops.Count)
        {
            yield return ($"{lens}.hops", $"{lens}.hops lists a hop kind twice.", "List each hop kind once.");
        }

        if (string.IsNullOrWhiteSpace(entry.MapVersion))
        {
            yield return ($"{lens}.mapVersion", $"{lens}.mapVersion is empty.", "Record the map version the lens read, for example v12.");
        }

        if (entry.FoundCount < entry.Nodes.Count + entry.Items.Count)
        {
            yield return ($"{lens}.foundCount", $"{lens}.foundCount is {entry.FoundCount.ToString(CultureInfo.InvariantCulture)}, fewer than the {(entry.Nodes.Count + entry.Items.Count).ToString(CultureInfo.InvariantCulture)} nodes and items listed.", "Record the full count the lens found.");
        }

        if (entry.Truncated.Distinct().Take(entry.Truncated.Count).Count() != entry.Truncated.Count)
        {
            yield return ($"{lens}.truncated", $"{lens}.truncated lists a band twice.", "List each truncated band once.");
        }
    }

    private static IEnumerable<(string Id, string Where)> Ids(string lens, LensEntry entry) =>
        entry.Nodes.Select((n, i) => (n.LensItem, $"{lens}.nodes[{i.ToString(CultureInfo.InvariantCulture)}].lensItem"))
            .Concat(entry.Items.Select((n, i) => (n.LensItem, $"{lens}.items[{i.ToString(CultureInfo.InvariantCulture)}].lensItem")));

    private static IEnumerable<(string Where, string Message, string Fix)> IdFailures(List<(string Name, LensEntry Entry)> lenses)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (lens, entry) in lenses)
        {
            foreach (var (id, where) in Ids(lens, entry))
            {
                if (IdShape.Match(id) is not { Success: true } match)
                {
                    continue;   // PKT-SCP-004 reports the shape
                }

                if (!string.Equals(match.Groups["lens"].Value, lens, StringComparison.Ordinal))
                {
                    yield return (where, $"{where} '{id}' sits in the {lens} lens but names the {match.Groups["lens"].Value} lens.", $"Number the {lens} lens's items L-{lens}-<n>.");
                }
                else if (!seen.Add(id))
                {
                    yield return (where, $"{where} '{id}' is used more than once.", "Give every found node and item its own id.");
                }
            }
        }
    }

    [GeneratedRegex("^L-(?<lens>code|product|related|history)-[1-9][0-9]*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdShape { get; }
}
