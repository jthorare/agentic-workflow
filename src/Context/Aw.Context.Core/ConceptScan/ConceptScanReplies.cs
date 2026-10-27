using System.Text.Json;

namespace Aw.Context.Core.ConceptScan;

/// <summary>
/// Reads the two model replies. Both are untrusted text: a reply that isn't JSON, a concept without a usable name, and a member that isn't a feature of the draft are dropped, never an error, so a bad answer scans nothing instead of failing the job.
/// There is no cap on the number of concepts. A reply cut off partway keeps every concept that arrived whole (see <see cref="IsCutOff"/>).
/// </summary>
public static class ConceptScanReplies
{
    private const int NameLength = 60;
    private const int QueryLength = 80;
    private const int QueriesPerConcept = 3;
    private const int EvidenceLength = 200;
    private const int EvidencePerConcept = 5;
    private const double MissingConfidence = 0.5;

    /// <summary>The concepts the first pass proposed.</summary>
    /// <param name="reply">The model's text: a JSON object {"concepts":[{"name":…,"queries":[…]}]} (or the array itself), possibly inside a code fence.</param>
    /// <returns>Every candidate with a name and at least one query, in the order given; empty for a reply that doesn't read.</returns>
    public static IReadOnlyList<ConceptCandidate> Candidates(string reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        var found = new List<ConceptCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Items(reply))
        {
            var queries = Strings(item, "queries", QueryLength).Take(QueriesPerConcept).ToList();
            if (Line(Text(item, "name"), NameLength) is { } name && queries.Count > 0 && seen.Add(name))
            {
                found.Add(new ConceptCandidate(name, queries));
            }
        }

        return found;
    }

    /// <summary>Whether a reply ended partway through its JSON while at least one concept before the break was whole.</summary>
    /// <param name="reply">The model's text.</param>
    /// <returns>True for a cut-off answer; false for one that reads whole, and for one that doesn't read at all.</returns>
    public static bool IsCutOff(string reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        return Read(reply).CutOff;
    }

    /// <summary>The concept hints the second pass returned.</summary>
    /// <param name="reply">The model's text: {"concepts":[{"name":…,"members":[…],"evidence":[…],"confidence":0.8}]}.</param>
    /// <param name="featureKeys">The keys of the draft's features ('feature:…'): the only members that count.</param>
    /// <returns>
    /// Every hint, best confidence first. A member that isn't a feature key is dropped, and so is a hint left with none. A feature stays in the first hint
    /// (by confidence) that names it.
    /// </returns>
    public static IReadOnlyList<ConceptHint> Hints(string reply, IReadOnlySet<string> featureKeys)
    {
        ArgumentNullException.ThrowIfNull(reply);
        ArgumentNullException.ThrowIfNull(featureKeys);
        var read = new List<ConceptHint>();
        foreach (var item in Items(reply))
        {
            var members = Strings(item, "members", int.MaxValue).Where(featureKeys.Contains).Distinct(StringComparer.Ordinal).ToList();
            if (Line(Text(item, "name"), NameLength) is { } name && members.Count > 0)
            {
                read.Add(new ConceptHint(name, members, [.. Strings(item, "evidence", EvidenceLength).Take(EvidencePerConcept)], Confidence(item)));
            }
        }

        return Disjoint(read);
    }

    // Best confidence first; a feature stays with the first hint that names it, and a hint left with no feature, or with a name another hint has, goes.
    private static List<ConceptHint> Disjoint(List<ConceptHint> read)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hints = new List<ConceptHint>();
        foreach (var hint in read.OrderByDescending(static h => h.Confidence).ThenBy(static h => h.Name, StringComparer.Ordinal))
        {
            var free = hint.Members.Where(taken.Add).ToList();
            if (free.Count > 0 && names.Add(hint.Name))
            {
                hints.Add(hint with { Members = free });
            }
        }

        return hints;
    }

    private static List<JsonElement> Items(string reply) => Read(reply).Items;

    // The concept objects of a reply: the root array, or the root object's 'concepts' array; nothing for text that isn't JSON. A reply that breaks off partway
    // (the model ran out of output tokens) keeps the objects that closed before the break, and says so.
    private static (List<JsonElement> Items, bool CutOff) Read(string reply)
    {
        try
        {
            using var document = JsonDocument.Parse(Unfenced(reply));
            var root = document.RootElement;
            var array = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("concepts", out var inner) ? inner : root;
            return (array.ValueKind == JsonValueKind.Array
                ? [.. array.EnumerateArray().Where(static i => i.ValueKind == JsonValueKind.Object).Select(static i => i.Clone())]
                : [], CutOff: false);
        }
        catch (JsonException)
        {
            var whole = Salvage(reply);
            return (whole, whole.Count > 0);   // nothing whole before the break: it doesn't read
        }
    }

    // The objects of the concepts array that closed before the text broke. Items sit at depth 1 in a root array and at depth 2 in a root object's 'concepts' array.
    private static List<JsonElement> Salvage(string reply)
    {
        var start = reply.IndexOfAny(['{', '[']);
        var kept = new List<JsonElement>();
        if (start < 0)
        {
            return kept;
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(reply[start..]);
        var itemDepth = bytes[0] == (byte)'[' ? 1 : 2;
        var reader = new Utf8JsonReader(bytes);
        var objectStart = -1;
        var inConcepts = itemDepth == 1;
        try
        {
            while (reader.Read())
            {
                Track(reader, (bytes, itemDepth), ref inConcepts, ref objectStart, kept);
            }
        }
        catch (JsonException)
        {
            // the break: what closed before it is kept
        }

        return kept;
    }

    private static void Track(Utf8JsonReader reader, (byte[] Bytes, int ItemDepth) text, ref bool inConcepts, ref int objectStart, List<JsonElement> kept)
    {
        if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
        {
            inConcepts = reader.ValueTextEquals("concepts");
        }
        else if (inConcepts && reader.CurrentDepth == text.ItemDepth && reader.TokenType == JsonTokenType.StartObject)
        {
            objectStart = (int)reader.TokenStartIndex;
        }
        else if (inConcepts && reader.CurrentDepth == text.ItemDepth && reader.TokenType == JsonTokenType.EndObject && objectStart >= 0)
        {
            using var item = JsonDocument.Parse(text.Bytes.AsMemory(objectStart, (int)reader.BytesConsumed - objectStart));
            kept.Add(item.RootElement.Clone());
            objectStart = -1;
        }
    }

    // A reply wrapped in a code fence, or with words around the JSON, still reads: from the first bracket to the last.
    private static string Unfenced(string reply)
    {
        var start = reply.IndexOfAny(['{', '[']);
        var end = reply.LastIndexOfAny(['}', ']']);
        return start >= 0 && end > start ? reply[start..(end + 1)] : reply;
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IEnumerable<string> Strings(JsonElement item, string name, int maxLength)
    {
        if (!item.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var entry in array.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String))
        {
            if (Line(entry.GetString(), maxLength) is { } line)
            {
                yield return line;
            }
        }
    }

    private static double Confidence(JsonElement item) =>
        item.TryGetProperty("confidence", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? Math.Round(Math.Clamp(number, 0, 1), 3, MidpointRounding.ToEven)
            : MissingConfidence;

    // One plain line: control characters and runs of spaces become one space; empty and over-long text is no text.
    private static string? Line(string? text, int maxLength)
    {
        var plain = new string([.. (text ?? string.Empty).Select(static c => char.IsControl(c) ? ' ' : c)]);
        var joined = string.Join(' ', plain.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return joined.Length == 0 || joined.Length > maxLength ? null : joined;
    }
}
