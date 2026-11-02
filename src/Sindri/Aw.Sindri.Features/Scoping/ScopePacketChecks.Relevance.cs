using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    private static readonly string[] _results = ["in", "out", "noise"];

    /// <summary>
    /// SCP-REL-001: fact:lenses holds exactly the entries the relevance review kept 'in' (a bypass or a stale pass differs), the counts agree with the entries, and an entry that left
    /// has an AI verdict and a judge ruling that both call it off topic, the judge's class as its result, and a citation that resolves. A disputed entry only warns, with both verdicts.
    /// </summary>
    private static void RelevanceKept(ScopeContext c, RuleSink s)
    {
        if (!s.Need("relevance", "lenses"))
        {
            return;
        }

        var entries = ScopeJson.Items(c.Facts["relevance"], "entries").ToList();
        var known = new HashSet<string>(entries.Select(static e => ScopeJson.Str(e, "id")).OfType<string>().Concat(entries.Select(static e => ScopeJson.Str(e, "key")).OfType<string>()), StringComparer.Ordinal);
        var kept = entries.Where(static e => string.Equals(ScopeJson.Str(e, "result"), "in", StringComparison.Ordinal)).Select(static e => ScopeJson.Str(e, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var id in c.Ids.Lens.Where(x => !kept.Contains(x)).Order(StringComparer.Ordinal))
        {
            s.Fail(id, $"{id} is in fact:lenses but the relevance review did not keep it in", "Run the relevance review and its apply Step over the lenses again; nothing else changes fact:lenses.");
        }

        foreach (var id in kept.Where(x => !c.Ids.Lens.Contains(x)).Order(StringComparer.Ordinal))
        {
            s.Fail(id, $"{id} was kept in by the relevance review but is not in fact:lenses", "The relevance record is from another pass; run the relevance review over the current lenses.");
        }

        Counts(c.Facts["relevance"], entries, s);
        foreach (var entry in entries)
        {
            Entry(entry, known, c.Ids, s);
        }
    }

    private static void Counts(JsonNode? relevance, List<JsonNode> entries, RuleSink s)
    {
        var counts = ScopeJson.Obj(relevance, "counts");
        var expected = new (string Name, int Value)[]
        {
            ("reviewed", entries.Count),
            ("in", ResultCount(entries, "in")),
            ("out", ResultCount(entries, "out")),
            ("noise", ResultCount(entries, "noise")),
            ("disputed", entries.Count(static e => ScopeJson.Bool(e, "disputed") == true)),
        };
        foreach (var (name, value) in expected.Where(e => ScopeJson.Int(counts, e.Name) != e.Value))
        {
            s.Fail("counts", string.Create(CultureInfo.InvariantCulture, $"fact:relevance counts.{name} is {ScopeJson.Int(counts, name)?.ToString(CultureInfo.InvariantCulture) ?? "missing"}, its entries give {value}"), "counts are computed from the entries by the apply Step.");
        }
    }

    private static int ResultCount(List<JsonNode> entries, string result) => entries.Count(e => string.Equals(ScopeJson.Str(e, "result"), result, StringComparison.Ordinal));

    private static void Entry(JsonNode entry, HashSet<string> known, ScopeIds ids, RuleSink s)
    {
        var id = ScopeJson.Str(entry, "id") ?? "?";
        var result = ScopeJson.Str(entry, "result");
        if (result is null || !_results.Contains(result, StringComparer.Ordinal))
        {
            s.Fail(id, $"{id} has the result {result ?? "none"}, not in, out or noise", "The apply Step gives every entry one of those results.");
            return;
        }

        var agent = ScopeJson.Str(ScopeJson.Obj(entry, "agent"), "class");
        var judge = ScopeJson.Str(ScopeJson.Obj(entry, "judge"), "class");
        Disputed(entry, id, (agent, judge), s);
        if (!string.Equals(result, "in", StringComparison.Ordinal))
        {
            var left = new Left(entry, id, result, agent, judge);
            if (Settled(left, s))
            {
                Cited(left, known, ids, s);
            }
        }
    }

    private static void Disputed(JsonNode entry, string id, (string? Agent, string? Judge) classes, RuleSink s)
    {
        if (ScopeJson.Bool(entry, "disputed") == true)
        {
            s.Warn(id, $"disputed: {ScopeJson.Str(entry, "label") ?? id} — AI said {classes.Agent ?? "nothing"}, judge said {classes.Judge ?? "nothing"}", "Read both reasons at the stop; the entry stayed in.");
        }
    }

    private const string LeftFix = "An entry leaves the lenses only when the AI and the judge both call it off topic and the AI cites something that resolves.";

    private sealed record Left(JsonNode Entry, string Id, string Result, string? Agent, string? Judge);

    // The AI and the judge both ruled, both called the entry off topic, and the judge's class is the result.
    private static bool Settled(Left left, RuleSink s)
    {
        var id = left.Id;
        if (ScopeJson.Obj(left.Entry, "agent") is null || ScopeJson.Obj(left.Entry, "judge") is null)
        {
            s.Fail(id, $"{id} left the lenses without both an AI verdict and a judge ruling", LeftFix);
            return false;
        }

        if (!OffTopic(left.Agent) || !OffTopic(left.Judge))
        {
            s.Fail(id, $"{id} left the lenses although the AI said {left.Agent ?? "nothing"} and the judge said {left.Judge ?? "nothing"}", LeftFix);
            return false;
        }

        if (!string.Equals(left.Result, left.Judge, StringComparison.Ordinal))
        {
            s.Fail(id, $"{id} left as {left.Result} but the judge ruled {left.Judge}", "The judge's class is the entry's result.");
            return false;
        }

        return true;
    }

    private static void Cited(Left left, HashSet<string> known, ScopeIds ids, RuleSink s)
    {
        var citation = ScopeJson.Str(ScopeJson.Obj(left.Entry, "agent"), "citation");
        if (string.IsNullOrWhiteSpace(citation) || !(ids.Resolves(citation) || known.Contains(citation)))
        {
            s.Fail(left.Id, $"{left.Id} left the lenses with the citation {citation ?? "none"}, which resolves to nothing", LeftFix);
        }
    }

    private static bool OffTopic(string? verdict) => string.Equals(verdict, "out", StringComparison.Ordinal) || string.Equals(verdict, "noise", StringComparison.Ordinal);
}
