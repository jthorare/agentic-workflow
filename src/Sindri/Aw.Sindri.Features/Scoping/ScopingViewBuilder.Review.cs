namespace Aw.Sindri.Features.Scoping;

/// <summary>The stop's rows for the relevance review and the gap search.</summary>
internal static partial class ScopingViewBuilder
{
    private static List<ScopingRow> DisputedRows(ScopeFacts facts)
    {
        var rows = new List<ScopingRow>();
        foreach (var entry in ScopeJson.Items(facts["relevance"], "entries").Where(static e => ScopeJson.Bool(e, "disputed") == true))
        {
            var id = ScopeJson.Str(entry, "id") ?? "?";
            var text = $"disputed: {ScopeJson.Str(entry, "label") ?? id} — AI said {ScopeJson.Str(ScopeJson.Obj(entry, "agent"), "class") ?? "nothing"}, judge said {ScopeJson.Str(ScopeJson.Obj(entry, "judge"), "class") ?? "nothing"}";
            rows.Add(new ScopingRow(id, text, "disputed", [.. new[] { ScopeJson.Str(ScopeJson.Obj(entry, "agent"), "reason") }.OfType<string>().Select(static r => $"AI: {r}")]));
        }

        return rows;
    }

    private static List<ScopingRow> GapRows(ScopeFacts facts)
    {
        var rows = new List<ScopingRow>();
        var rounds = ScopeJson.Items(facts["gapSearch"], "rounds").ToList();
        rows.AddRange(rounds.SelectMany(static r => ScopeJson.Items(r, "candidates"))
            .Where(static c => string.Equals(ScopeJson.Str(c, "ruling"), "uncertain", StringComparison.Ordinal))
            .Select(static c => new ScopingRow(ScopeJson.Str(c, "id") ?? "?", $"uncertain: {ScopeJson.Str(c, "title")} ({ScopeJson.Str(c, "citation")})", "uncertain", [])));
        if (rounds.Count > 0)
        {
            rows.AddRange(ScopeJson.Items(rounds[^1], "candidates")
                .Where(static c => string.Equals(ScopeJson.Str(c, "ruling"), "real-gap", StringComparison.Ordinal))
                .Select(static c => new ScopingRow(ScopeJson.Str(c, "id") ?? "?", $"open gap: {ScopeJson.Str(c, "title")} ({ScopeJson.Str(c, "citation")})", "open-gap", [])));
        }

        return rows;
    }
}
