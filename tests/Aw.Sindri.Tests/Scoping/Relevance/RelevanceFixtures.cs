using System.Text.Json.Nodes;

namespace Aw.Sindri.Tests.Scoping.Relevance;

/// <summary>The small lenses, coverage and brief the relevance tests share, and builders for the agent's verdicts and the judge's rulings (spec sections 4.1 to 4.3).</summary>
internal static class RelevanceFixtures
{
    /// <summary>The distinctive texts a verdict carries, which no judge request may hold.</summary>
    internal const string ReasonMarker = "REASON-MARKER-4f1c";
    internal const string CitationMarker = "CITATION-MARKER-9b2e";
    internal const string RelabelMarker = "feature:relabel-marker-7d3a";
    internal const string ModelMarker = "agent-model-marker-2a8f";

    internal const string BriefText = "Invitations expire after a chosen number of days.";

    internal static readonly string[] AllIds =
        ["L-code-1", "L-code-2", "L-code-3", "L-product-1", "L-product-2", "L-history-1"];

    /// <summary>Code: a shared shell, the expiry writer and a billing export; product: the invitations feature and the billing export; history: one decision (no node key).</summary>
    internal static JsonObject Lenses() => new()
    {
        ["code"] = Lens(
            ["surface:screen/invite-dialog"], 3,
            [Node("code:src/admin/Shell.ts", "code", "Shell.ts", "code", "L-code-1"), Node("code:src/invites/expiry.ts", "code", "expiry.ts", "code", "L-code-2"), Node("code:src/billing/export.ts", "code", "export.ts", "code", "L-code-3")],
            [],
            [Edge("code:src/invites/expiry.ts", "code:src/admin/Shell.ts"), Edge("code:src/invites/expiry.ts", "surface:screen/invite-dialog"), Edge("code:src/billing/export.ts", "code:src/invites/expiry.ts")]),
        ["product"] = Lens(
            ["surface:screen/invite-dialog"], 2,
            [Node("feature:invitations", "feature", "Invitations", "product", "L-product-1"), Node("feature:billing-export", "feature", "Billing export", "product", "L-product-2")],
            [],
            []),
        ["related"] = Lens(["surface:screen/invite-dialog"], 0, [], [], []),
        ["history"] = Lens(["surface:screen/invite-dialog"], 1, [], [Item("decision", "Decision 12", "L-history-1")], []),
    };

    /// <summary>Coverage rows as the gather writes them for <see cref="Lenses"/>; history carries one recorded limit.</summary>
    internal static JsonObject Coverage() => new()
    {
        ["lenses"] = new JsonArray(
            Row("code", "found", 3, ["L-code-1", "L-code-2", "L-code-3"], limited: false),
            Row("product", "found", 2, ["L-product-1", "L-product-2"], limited: false),
            Row("related", "nothing-found", 0, [], limited: false),
            Row("history", "found", 1, ["L-history-1"], limited: true)),
    };

    internal static JsonObject Brief() => new()
    {
        ["text"] = BriefText,
        ["refs"] = new JsonArray(new JsonObject { ["id"] = "R1", ["source"] = "brief", ["key"] = "briefs/invites.md", ["version"] = "v1", ["trust"] = "untrusted", ["fetchedAt"] = "2026-10-24T00:00:00Z" }),
        ["subject"] = "Invitation expiry",
    };

    /// <summary>A verdict as the review Step writes it. <paramref name="fix"/> is required unless the class is 'in'.</summary>
    internal static JsonObject Verdict(string id, string cls, string? citation = null, string? fix = null, string? relabelTo = null)
    {
        var verdict = new JsonObject { ["id"] = id, ["class"] = cls, ["reason"] = $"{ReasonMarker} for {id}", ["citation"] = citation ?? id };
        if (cls != "in")
        {
            verdict["fix"] = fix ?? "ignore";
            if (relabelTo is not null)
            {
                verdict["relabelTo"] = relabelTo;
            }
        }

        return verdict;
    }

    internal static JsonObject Verdicts(params JsonObject[] verdicts) => new() { ["model"] = ModelMarker, ["verdicts"] = new JsonArray([.. verdicts]) };

    /// <summary>A judge ruling for an id; <paramref name="cls"/> is in, out, noise or uncertain.</summary>
    internal static JsonObject Ruling(string id, string cls) => new()
    {
        ["id"] = id,
        ["class"] = cls,
        ["confidence"] = cls == "uncertain" ? null : 0.9,
        ["provider"] = cls == "uncertain" ? null : "jev",
        ["decisionId"] = $"JD-{id}",
        ["reasonCode"] = cls == "uncertain" ? "below-threshold" : "model",
    };

    internal static JsonObject Rulings(params JsonObject[] rulings) => new()
    {
        ["judge"] = new JsonObject { ["providers"] = new JsonArray("jev"), ["threshold"] = 0.6 },
        ["rulings"] = new JsonArray([.. rulings]),
    };

    /// <summary>Verdicts that say 'in' for every id except the given ones, and the matching rulings.</summary>
    internal static (JsonObject Verdicts, JsonObject Rulings) AllIn(params (JsonObject Verdict, JsonObject Ruling)[] except)
    {
        var skipped = except.Select(static e => e.Verdict["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var verdicts = AllIds.Where(id => !skipped.Contains(id)).Select(static id => Verdict(id, "in")).Concat(except.Select(static e => e.Verdict)).ToArray();
        var rulings = AllIds.Where(id => !skipped.Contains(id)).Select(static id => Ruling(id, "in")).Concat(except.Select(static e => e.Ruling)).ToArray();
        return (Verdicts(verdicts), Rulings(rulings));
    }

    internal static JsonObject Node(string key, string kind, string label, string band, string lensItem) =>
        new() { ["key"] = key, ["kind"] = kind, ["label"] = label, ["band"] = band, ["lensItem"] = lensItem };

    internal static JsonObject Item(string kind, string label, string lensItem) =>
        new() { ["lensItem"] = lensItem, ["kind"] = kind, ["label"] = label, ["citations"] = new JsonArray("12") };

    internal static JsonObject Edge(string from, string to) =>
        new() { ["from"] = from, ["to"] = to, ["type"] = "relates", ["provenance"] = "deterministic", ["confirmed"] = false };

    internal static JsonObject Lens(string[] anchors, int foundCount, JsonObject[] nodes, JsonObject[] items, JsonObject[] edges) => new()
    {
        ["anchors"] = new JsonArray([.. anchors.Select(static a => (JsonNode?)JsonValue.Create(a))]),
        ["depth"] = 0,
        ["hops"] = new JsonArray(),
        ["mapVersion"] = "v1",
        ["foundCount"] = foundCount,
        ["nodes"] = new JsonArray([.. nodes]),
        ["edges"] = new JsonArray([.. edges]),
        ["items"] = new JsonArray([.. items]),
        ["truncated"] = new JsonArray(),
    };

    private static JsonObject Row(string lens, string state, int count, string[] ids, bool limited) => new()
    {
        ["lens"] = lens,
        ["state"] = state,
        ["searched"] = new JsonObject { ["anchors"] = new JsonArray("surface:screen/invite-dialog"), ["sources"] = new JsonArray(), ["mapVersion"] = "v1", ["depth"] = 0, ["hops"] = new JsonArray() },
        ["limits"] = limited ? new JsonArray(new JsonObject { ["not"] = "tickets linked to the map", ["why"] = "history-unlinked" }) : [],
        ["mapCoverage"] = new JsonObject { ["mapped"] = count, ["new"] = 0, ["unmapped"] = 0 },
        ["found"] = new JsonObject { ["count"] = count, ["ids"] = new JsonArray([.. ids.Select(static i => (JsonNode?)JsonValue.Create(i))]) },
    };
}
