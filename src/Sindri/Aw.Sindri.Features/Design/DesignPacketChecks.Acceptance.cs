using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

internal static partial class DesignPacketChecks
{
    private static readonly string[] _evidenceKinds = ["test", "ui-browser", "api-transcript", "cli-transcript", "ios-sim", "migration", "a11y", "perf", "behavior-sim", "design-parity", "db-rows"];
    private static readonly string[] _places = ["head", "base+head"];

    [GeneratedRegex(@"\b(fast|quick|clean|intuitive|easy|seamless|robust|user-friendly|smooth|simple|snappy)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Unmeasurable { get; }

    [GeneratedRegex(@"\d|'[^']+'", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NumberOrReference { get; }

    /// <summary>DSN-ACC-001: every live acceptance item has at least one proof signal of a known evidence kind, an observable statement, and where it runs.</summary>
    internal static void ItemsHaveSignals(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("acceptance.json", "signals.json"))
        {
            return;
        }

        var signals = LineJson.Obj(facts.Json("signals.json"), "signals");
        foreach (var item in Items(facts).Where(Live))
        {
            var id = LineJson.Str(item, "id") ?? string.Empty;
            var own = LineJson.Items(signals, id).ToList();
            if (own.Count == 0)
            {
                sink.Fail(id, $"{id} has no proof signal", "add a signal: an evidence kind, an observable statement and where it runs");
            }

            foreach (var signal in own)
            {
                CheckSignalShape(sink, LineJson.Str(signal, "id") ?? id, signal);
            }
        }
    }

    private static void CheckSignalShape(LineSink sink, string name, JsonNode signal)
    {
        if (Array.IndexOf(_evidenceKinds, LineJson.Str(signal, "kind")) < 0)
        {
            sink.Fail(name, $"{name} has the evidence kind '{LineJson.Str(signal, "kind")}'", $"use one of {string.Join(", ", _evidenceKinds)}");
        }

        if (Array.IndexOf(_places, LineJson.Str(signal, "runsAt")) < 0)
        {
            sink.Fail(name, $"{name} says it runs at '{LineJson.Str(signal, "runsAt")}'", "say 'head', or 'base+head' for a fix");
        }

        if (string.IsNullOrWhiteSpace(LineJson.Str(signal, "statement")))
        {
            sink.Fail(name, $"{name} has no statement", "state what is observed");
        }
    }

    /// <summary>DSN-ACC-002: signals are measurable: an unmeasurable term is refused, and a statement names a number or quotes a reference artifact.</summary>
    internal static void SignalsMeasurable(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("signals.json"))
        {
            return;
        }

        foreach (var (id, statement) in Statements(facts))
        {
            if (Unmeasurable.Match(statement) is { Success: true } term)
            {
                sink.Fail(id, $"{id} uses the unmeasurable term '{term.Value}'", "replace it with a number or a reference artifact");
            }
            else if (!NumberOrReference.IsMatch(statement))
            {
                sink.Fail(id, $"{id} names no number and quotes no reference", "state a number, or quote the text or artifact the check compares with");
            }
        }
    }

    private static IEnumerable<(string Id, string Statement)> Statements(LineFacts facts)
    {
        if (LineJson.Obj(facts.Json("signals.json"), "signals") is not { } signals)
        {
            yield break;
        }

        foreach (var (item, list) in signals)
        {
            foreach (var signal in LineJson.Items(list))
            {
                yield return (LineJson.Str(signal, "id") ?? item, LineJson.Str(signal, "statement") ?? string.Empty);
            }
        }
    }

    /// <summary>DSN-ACC-003 (LC-ACC-001): scope's ids and texts unchanged; every added item cites a section or decision; every superseded item names its successor and is flagged for stop 2; only the proof pack flips a status.</summary>
    internal static void AcceptanceChain(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("acceptance.json", ScopeAcceptance, "sections.json", "decisions.json"))
        {
            return;
        }

        var items = Items(facts).ToList();
        var byId = ById(items);
        var scope = LineJson.Items(facts.Json(ScopeAcceptance), "items").ToList();
        ScopeItemsKept(sink, scope, byId);
        var scopeIds = Ids(scope);
        var targets = Ids(Sections(facts)).Concat(Ids(Decisions(facts))).ToHashSet(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var id = LineJson.Str(item, "id") ?? string.Empty;
            CheckStatus(sink, item, id, byId);
            if (!scopeIds.Contains(id) && !LineJson.Strings(item, "citations").Any(targets.Contains))
            {
                sink.Fail(id, $"{id} was added and cites no section or decision of this design", "cite the section or decision that adds it");
            }
        }
    }

    private static void ScopeItemsKept(LineSink sink, List<JsonNode> scope, Dictionary<string, JsonNode> byId)
    {
        foreach (var original in scope)
        {
            var id = LineJson.Str(original, "id") ?? string.Empty;
            if (!byId.TryGetValue(id, out var kept))
            {
                sink.Fail(id, $"{id} was in the scope packet and is gone", "keep every scope item; supersede one with supersededBy instead of deleting it");
            }
            else if (!string.Equals(LineJson.Str(kept, "text"), LineJson.Str(original, "text"), StringComparison.Ordinal))
            {
                sink.Fail(id, $"{id}'s text differs from the scope packet's", "ids and texts never change; supersede the item and add a new one");
            }
        }
    }

    private static void CheckStatus(LineSink sink, JsonNode item, string id, Dictionary<string, JsonNode> byId)
    {
        if (string.Equals(LineJson.Str(item, "status"), "passing", StringComparison.Ordinal))
        {
            sink.Fail(id, $"{id} is marked passing in a design", "only the proof pack flips an item to passing");
        }

        if (Live(item))
        {
            return;
        }

        var successor = LineJson.Str(item, "supersededBy");
        if (successor is null || !byId.ContainsKey(successor) || string.Equals(successor, id, StringComparison.Ordinal))
        {
            sink.Fail(id, $"{id} is superseded with no successor in this design", "name the one item that replaces it in supersededBy");
        }
        else
        {
            sink.Warn(id, $"{id} is superseded by {successor}; stop 2 confirms it", "the owner confirms the supersession at stop 2");
        }
    }
}

