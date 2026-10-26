using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>
    /// SCP-GAP-001: the gap search ran, and its last round confirmed no gap. A confirmed gap fails while the gap loop has rounds left (the stage goes back through anchors, gather and draft)
    /// and is an open warning once the loop is spent. A candidate the judge could not rule on is a warning.
    /// </summary>
    private static void GapSearched(ScopeContext c, RuleSink s)
    {
        if (!s.Need("gapSearch"))
        {
            return;
        }

        var rounds = ScopeJson.Items(c.Facts["gapSearch"], "rounds").ToList();
        if (rounds.Count == 0)
        {
            s.Fail(item: null, "the gap search has not run", "Run the gap search; its last round must confirm no gap.");
            return;
        }

        var spent = rounds.Count >= c.GapCap;
        foreach (var candidate in Candidates(rounds[^1]).Where(static x => IsRuling(x, "real-gap")))
        {
            var message = $"the gap search confirmed a gap the map misses: {ScopeJson.Str(candidate, "title")} ({ScopeJson.Str(candidate, "citation")})";
            if (spent)
            {
                s.Warn(ScopeJson.Str(candidate, "id"), message + " and the gap loop is spent", "Read it at the stop; it is open.");
            }
            else
            {
                s.Fail(ScopeJson.Str(candidate, "id"), message, "Add it to the map, or exclude it with a reason, then check again.");
            }
        }

        foreach (var candidate in rounds.SelectMany(Candidates).Where(static x => IsRuling(x, "uncertain")))
        {
            s.Warn(ScopeJson.Str(candidate, "id"), $"the judge could not rule on {ScopeJson.Str(candidate, "title")} ({ScopeJson.Str(candidate, "citation")})", "Decide at the stop whether it belongs in the scope.");
        }
    }

    private static IEnumerable<JsonNode> Candidates(JsonNode round) => ScopeJson.Items(round, "candidates");

    private static bool IsRuling(JsonNode candidate, string ruling) => string.Equals(ScopeJson.Str(candidate, "ruling"), ruling, StringComparison.Ordinal);
}
