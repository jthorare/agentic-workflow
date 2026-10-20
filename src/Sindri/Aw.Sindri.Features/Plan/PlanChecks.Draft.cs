using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

internal static partial class PlanChecks
{
    private static readonly string[] _sizes = ["XS", "S", "M", "L", "XL"];
    private static readonly string[] _realApp = ["ui-browser", "api-transcript", "cli-transcript", "ios-sim"];

    /// <summary>PLN-PKT-001: aw-packets validate passes the plan packet (PKT-ID, WS, GRAPH, BASE, SCOPE, ACC, EVID, DEC, HOLD, SIZE).</summary>
    internal static void PacketValidates(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("validation.json"))
        {
            return;
        }

        var validation = facts.Json("validation.json");
        if (LineJson.Bool(validation, "passed") == true)
        {
            return;
        }

        var failures = LineJson.Items(validation, "failures").ToList();
        if (failures.Count == 0)
        {
            sink.Fail(item: null, "aw-packets validate did not pass the plan packet", "run aw-packets validate on the plan packet and fix what it names");
        }

        foreach (var failure in failures)
        {
            var rule = LineJson.Str(failure, "rule") ?? "?";
            sink.Fail(rule, $"{rule} ({LineJson.Str(failure, "subject")}): {LineJson.Str(failure, "message")}", LineJson.Str(failure, "fix") ?? "fix the packet");
        }
    }

    /// <summary>PLN-ACC-001: every live acceptance item of the design is in exactly one phase, with its text unchanged.</summary>
    internal static void ItemsInOnePhase(LineFacts facts, LineSink sink)
    {
        if (!sink.Need(Tasks, "design/acceptance.json"))
        {
            return;
        }

        var live = LiveDesignItems(facts).ToDictionary(static i => LineJson.Str(i, "id") ?? string.Empty, static i => LineJson.Str(i, "text"), StringComparer.Ordinal);
        var owners = live.Keys.ToDictionary(static k => k, static _ => new List<string>(), StringComparer.Ordinal);
        foreach (var phase in Phases(facts))
        {
            foreach (var item in OwnItems(phase))
            {
                var id = LineJson.Str(item, "id") ?? string.Empty;
                if (!live.TryGetValue(id, out var text))
                {
                    sink.Fail(id, $"{PhaseId(phase)} carries {id}, which is no live item of the design", "carry only the design's live acceptance items");
                    continue;
                }

                owners[id].Add(PhaseId(phase));
                if (!string.Equals(LineJson.Str(item, "text"), text, StringComparison.Ordinal))
                {
                    sink.Fail(id, $"{PhaseId(phase)} words {id} differently from the design", "ids and texts never change");
                }
            }
        }

        foreach (var (id, phases) in owners.Where(static o => o.Value.Count != 1))
        {
            sink.Fail(id, phases.Count == 0 ? $"{id} is in no phase" : $"{id} is in {phases.Count.ToString(CultureInfo.InvariantCulture)} phases ({string.Join(", ", phases)})", "put every live item in exactly one phase");
        }
    }

    /// <summary>PLN-SIZE-001: every phase is at most workflow.plan.maxPhaseSize (the files, items and budget limits of a size are aw-packets' PKT-SIZE-001).</summary>
    internal static void PhasesWithinSize(LineFacts facts, LineSink sink)
    {
        if (!sink.Need(Tasks))
        {
            return;
        }

        var cap = Array.IndexOf(_sizes, MaxPhaseSize(facts));
        foreach (var phase in Phases(facts))
        {
            var size = LineJson.Str(phase, "size");
            var index = Array.IndexOf(_sizes, size);
            if (index < 0 || index > cap)
            {
                sink.Fail(PhaseId(phase), $"{PhaseId(phase)} is size '{size}', over the cap {MaxPhaseSize(facts)}", "split the phase until each part is within the cap");
            }
        }
    }

    /// <summary>PLN-SIG-001: every item of a fix phase has a base+head signal; every phase has a real-app signal unless it is a docs or config phase.</summary>
    internal static void PhasesHaveSignals(LineFacts facts, LineSink sink)
    {
        if (!sink.Need(Tasks, "design/signals.json"))
        {
            return;
        }

        var signals = LineJson.Obj(facts.Json("design/signals.json"), "signals");
        foreach (var phase in Phases(facts))
        {
            var id = PhaseId(phase);
            var kind = LineJson.Strings(phase, "rules").Select(static r => r.StartsWith("kind:", StringComparison.Ordinal) ? r[5..] : null).OfType<string>().FirstOrDefault();
            if (kind is null)
            {
                sink.Fail(id, $"{id} has no kind rule", "add a 'kind:feature|fix|refactor|docs|config' rule");
                continue;
            }

            if (string.Equals(kind, "fix", StringComparison.Ordinal))
            {
                FixItemsCompared(sink, phase, signals);
            }

            if (kind is not ("docs" or "config") && !OwnItems(phase).SelectMany(i => LineJson.Items(signals, LineJson.Str(i, "id") ?? string.Empty)).Any(s => Array.IndexOf(_realApp, LineJson.Str(s, "kind")) >= 0))
            {
                sink.Fail(id, $"{id} has no signal of a real-app kind ({string.Join(", ", _realApp)})", "add a signal that runs the real app, or make the phase docs or config");
            }
        }
    }

    private static void FixItemsCompared(LineSink sink, JsonNode phase, JsonObject? signals)
    {
        foreach (var item in OwnItems(phase))
        {
            var itemId = LineJson.Str(item, "id") ?? string.Empty;
            if (!LineJson.Items(signals, itemId).Any(static s => string.Equals(LineJson.Str(s, "runsAt"), "base+head", StringComparison.Ordinal)))
            {
                sink.Fail(PhaseId(phase), $"{PhaseId(phase)} is a fix and {itemId} has no base+head signal", "a fix proves fail-before and pass-after: add a base+head signal");
            }
        }
    }

    /// <summary>PLN-STACK-001: a phase with more than one dependency waits for a merge; a stack is at most workflow.line.maxStack deep.</summary>
    internal static void StacksStayShallow(LineFacts facts, LineSink sink)
    {
        if (!sink.Need(Tasks, "graph.json"))
        {
            return;
        }

        var deps = Phases(facts).ToDictionary(PhaseId, static p => LineJson.Strings(p, "deps").ToList(), StringComparer.Ordinal);
        var graph = LineJson.Items(facts.Json("graph.json"), "phases").ToDictionary(PhaseId, static p => p, StringComparer.Ordinal);
        foreach (var (id, own) in deps)
        {
            if (own.Exists(d => !deps.ContainsKey(d) || string.Equals(d, id, StringComparison.Ordinal)))
            {
                sink.Fail(id, $"{id} depends on a phase that does not exist or on itself", "depend only on the plan's other phases");
            }
            else if (own.Count > 1 && !(graph.TryGetValue(id, out var node) && LineJson.Bool(node, "waitsForMerge") == true))
            {
                sink.Fail(id, $"{id} builds on {own.Count.ToString(CultureInfo.InvariantCulture)} phases and is not marked waitsForMerge", "mark the phase waitsForMerge in graph.json");
            }

            var depth = Depth(id, deps, []);
            if (depth > MaxStack(facts))
            {
                sink.Fail(id, $"{id} sits {depth.ToString(CultureInfo.InvariantCulture)} deep in its stack, over {MaxStack(facts).ToString(CultureInfo.InvariantCulture)}", "cut the stack or merge earlier phases first");
            }
        }
    }

    // The longest chain of dependencies ending at the phase; a loop or an unknown dependency stops the walk.
    private static int Depth(string id, Dictionary<string, List<string>> deps, HashSet<string> path)
    {
        if (!deps.TryGetValue(id, out var own) || !path.Add(id))
        {
            return 0;
        }

        var deepest = own.Select(d => Depth(d, deps, path)).DefaultIfEmpty(0).Max();
        _ = path.Remove(id);
        return 1 + deepest;
    }
}
