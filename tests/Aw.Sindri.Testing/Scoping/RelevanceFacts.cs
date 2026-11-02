using System.Text.Json.Nodes;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>Edits of the gold facts for the relevance review (issue 966): one entry's two verdicts and what the pass did to the lenses and coverage, kept consistent so only the rule under test is broken.</summary>
public static class RelevanceFacts
{
    private static readonly string[] _lists = ["nodes", "items"];

    /// <summary>
    /// Sets an entry's verdicts and result in 'fact:relevance' and recomputes its counts. An entry whose result is 'out' or 'noise' also leaves 'fact:lenses'
    /// (with the edges that touched its node) and 'fact:coverage' (its found id and count), as the apply Step does.
    /// </summary>
    /// <param name="facts">The facts, edited in place.</param>
    /// <param name="id">The entry's lens item id.</param>
    /// <param name="agent">The AI's class: in, out or noise.</param>
    /// <param name="judge">The judge's class: in, out, noise or uncertain.</param>
    /// <param name="citation">The AI's citation (ignored when it said 'in').</param>
    /// <param name="result">The class the entry ended with.</param>
    /// <param name="disputed">Whether the entry is flagged as disputed.</param>
    /// <returns>The same facts, for chaining.</returns>
    public static ScopingFacts Rule(this ScopingFacts facts, string id, string agent, string judge, string citation, string result, bool disputed = false)
    {
        ArgumentNullException.ThrowIfNull(facts);
        string? lens = null;
        string? key = null;
        _ = facts.Edit("relevance", n =>
        {
            var entries = n["entries"]!.AsArray();
            var entry = entries.Single(e => (string?)e!["id"] == id)!;
            lens = (string?)entry["lens"];
            key = (string?)entry["key"];
            var seen = entry["agent"]!.AsObject();
            seen["class"] = agent;
            seen["citation"] = citation;
            _ = seen.Remove("fix");
            if (agent != "in")
            {
                seen["fix"] = "ignore";
            }

            entry["judge"]!["class"] = judge;
            entry["result"] = result;
            entry["disputed"] = disputed;
            n["counts"] = Counts(entries);
        });
        if (string.Equals(result, "out", StringComparison.Ordinal) || string.Equals(result, "noise", StringComparison.Ordinal))
        {
            Leave(facts, lens!, key, id);
        }

        return facts;
    }

    /// <summary>Removes an entry's 'judge' ruling, as a pass that never asked the judge.</summary>
    /// <param name="facts">The facts, edited in place.</param>
    /// <param name="id">The entry's lens item id.</param>
    /// <returns>The same facts, for chaining.</returns>
    public static ScopingFacts WithoutJudge(this ScopingFacts facts, string id)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Edit("relevance", n => ((JsonObject)n["entries"]!.AsArray().Single(e => (string?)e!["id"] == id)!).Remove("judge"));
    }

    private static JsonObject Counts(JsonArray entries) => new()
    {
        ["reviewed"] = entries.Count,
        ["in"] = entries.Count(static e => (string?)e!["result"] == "in"),
        ["out"] = entries.Count(static e => (string?)e!["result"] == "out"),
        ["noise"] = entries.Count(static e => (string?)e!["result"] == "noise"),
        ["disputed"] = entries.Count(static e => (bool?)e!["disputed"] == true),
    };

    private static void Leave(ScopingFacts facts, string lens, string? key, string id)
    {
        _ = facts.Edit("lenses", n =>
        {
            var entry = n[lens]!.AsObject();
            foreach (var list in _lists)
            {
                var array = entry[list]!.AsArray();
                var found = array.FirstOrDefault(e => (string?)e!["lensItem"] == id);
                if (found is not null)
                {
                    _ = array.Remove(found);
                }
            }

            var edges = entry["edges"]!.AsArray();
            foreach (var edge in edges.Where(e => key is not null && ((string?)e!["from"] == key || (string?)e!["to"] == key)).ToList())
            {
                _ = edges.Remove(edge);
            }

            entry["foundCount"] = (int)entry["foundCount"]! - 1;
        });
        _ = facts.Edit("coverage", n =>
        {
            var row = n["lenses"]!.AsArray().Single(r => (string?)r!["lens"] == lens)!;
            var ids = row["found"]!["ids"]!.AsArray();
            var gone = ids.Single(i => (string?)i == id);
            _ = ids.Remove(gone);
            row["found"]!["count"] = ids.Count;
            if (key is not null)
            {
                row["mapCoverage"]!["mapped"] = (int)row["mapCoverage"]!["mapped"]! - 1;
            }
        });
    }
}
