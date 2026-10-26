using System.Globalization;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Features.Line;

internal static partial class PlanChecks
{
    private static readonly string[] _sizes = ["XS", "S", "M", "L", "XL"];

    // PLN-ACC-001: every live acceptance item of the design is in exactly one phase.
    private static IEnumerable<LineFinding> Trace(PlanContext c)
    {
        var placed = c.Phases.SelectMany(p => PlanContext.Items(p).Select(a => (Item: LineJson.Str(a, "id"), Phase: PlanContext.Id(p)))).ToList();
        foreach (var item in c.LiveItems)
        {
            var phases = placed.Where(x => string.Equals(x.Item, item, StringComparison.Ordinal)).Select(static x => x.Phase).Distinct(StringComparer.Ordinal).ToList();
            if (phases.Count != 1)
            {
                yield return DesignContext.Fail("PLN-ACC-001", item, phases.Count == 0 ? $"acceptance item {item} is in no phase" : $"acceptance item {item} is in {phases.Count.ToString(CultureInfo.InvariantCulture)} phases ({string.Join(", ", phases)})", "Put every live item in exactly one phase.");
            }
        }
    }

    // PLN-SIZE-001
    private static IEnumerable<LineFinding> Sizes(PlanContext c)
    {
        var max = Array.IndexOf(_sizes, LineJson.Str(c.Plan, "maxPhaseSize") ?? "M");
        max = Math.Min(max < 0 ? 2 : max, 2);   // L and XL are refused whatever the profile says (LN5)
        foreach (var phase in c.Phases)
        {
            var size = LineJson.Str(phase, "size") ?? string.Empty;
            var at = Array.IndexOf(_sizes, size);
            if (at < 0 || at > max)
            {
                yield return DesignContext.Fail("PLN-SIZE-001", PlanContext.Id(phase), $"phase {PlanContext.Id(phase)} has the size '{size}', above the most a phase may be ({_sizes[max]})", "Split the phase.");
            }
        }
    }

    // PLN-STACK-001: more than one unmerged dependency waits for a merge; a stack is at most maxStack deep.
    private static IEnumerable<LineFinding> Stacks(PlanContext c)
    {
        var limit = LineJson.Int(c.Plan, "maxStack") ?? 4;
        foreach (var phase in c.Phases.Where(static p => LineJson.Strings(p, "deps").Skip(1).Any() && LineJson.Bool(p, "waitsForMerge") != true))
        {
            yield return DesignContext.Fail("PLN-STACK-001", PlanContext.Id(phase), $"phase {PlanContext.Id(phase)} has more than one dependency and is not marked waitsForMerge", "Mark it waitsForMerge.");
        }

        foreach (var phase in c.Phases.Where(p => Depth(c, PlanContext.Id(p), []) > limit))
        {
            yield return DesignContext.Fail("PLN-STACK-001", PlanContext.Id(phase), $"phase {PlanContext.Id(phase)} sits {Depth(c, PlanContext.Id(phase), []).ToString(CultureInfo.InvariantCulture)} deep in a stack; the most is {limit.ToString(CultureInfo.InvariantCulture)}", "Reorder the phases or let one wait for a merge.");
        }
    }

    // PLN-TEST-001: BLD-TEST-001 fails a build that changes an existing test its phase doesn't name in testChange, and the builder can't fix that, so the plan names every
    // existing test file it allows. A path the phase lists is taken as existing; 'new: path' entries and globs are not (the plan can't know what a glob matches).
    private static IEnumerable<LineFinding> TestsNamed(PlanContext c)
    {
        foreach (var phase in c.Phases)
        {
            var named = PlanPacket.TestChanges(phase).Select(static t => LineJson.Str(t, "path")).OfType<string>().ToList();
            var allowed = TestPaths.AllowedEntries([.. LineJson.Strings(phase, "writeScope")], [.. LineJson.Strings(phase, "allowedFiles")]);
            foreach (var path in TestPaths.UnnamedExisting(allowed, named))
            {
                yield return DesignContext.Fail("PLN-TEST-001", PlanContext.Id(phase), $"phase {PlanContext.Id(phase)} allows the existing test {path} but its testChange doesn't name it", $"Add {path} with a reason to the phase's testChange, or write 'new: {path}' if the phase creates it.");
            }
        }
    }

    // The longest chain of dependencies ending at the phase, itself included; a cycle stops the walk (PKT-GRAPH-001 reports it).
    private static int Depth(PlanContext c, string id, HashSet<string> seen)
    {
        if (!seen.Add(id) || c.Phases.FirstOrDefault(p => string.Equals(PlanContext.Id(p), id, StringComparison.Ordinal)) is not { } phase)
        {
            return 0;
        }

        return 1 + LineJson.Strings(phase, "deps").Select(d => Depth(c, d, [.. seen])).DefaultIfEmpty(0).Max();
    }
}
