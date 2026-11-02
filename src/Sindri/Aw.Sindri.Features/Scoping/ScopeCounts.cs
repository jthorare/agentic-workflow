using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The counts shown with the packet and in the stop's header line (issue 966 section 5.1), computed from the two facts by code and never read from the counts a model wrote.</summary>
internal static class ScopeCounts
{
    /// <summary>The relevance counts, 'relevance: 29 in, 0 out, 0 noise, 0 disputed'.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The text, or null when fact:relevance is absent.</returns>
    internal static string? Relevance(ScopeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!facts.Has("relevance"))
        {
            return null;
        }

        var entries = ScopeJson.Items(facts["relevance"], "entries").ToList();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"relevance: {Result(entries, "in")} in, {Result(entries, "out")} out, {Result(entries, "noise")} noise, {entries.Count(static e => ScopeJson.Bool(e, "disputed") == true)} disputed");
    }

    /// <summary>The gap search counts, 'gap search: 1 round, 4 found, 1 confirmed'.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The text, or null when fact:gapSearch is absent.</returns>
    internal static string? Gap(ScopeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!facts.Has("gapSearch"))
        {
            return null;
        }

        var rounds = ScopeJson.Items(facts["gapSearch"], "rounds").ToList();
        var candidates = rounds.SelectMany(static r => ScopeJson.Items(r, "candidates")).ToList();
        var confirmed = candidates.Count(static c => string.Equals(ScopeJson.Str(c, "ruling"), "real-gap", StringComparison.Ordinal));
        return string.Create(CultureInfo.InvariantCulture, $"gap search: {rounds.Count} {(rounds.Count == 1 ? "round" : "rounds")}, {candidates.Count} found, {confirmed} confirmed");
    }

    private static int Result(List<JsonNode> entries, string result) => entries.Count(e => string.Equals(ScopeJson.Str(e, "result"), result, StringComparison.Ordinal));
}
