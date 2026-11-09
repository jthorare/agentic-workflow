namespace Aw.Sindri.Features.RetireInstancesOnStart;

/// <summary>
/// The end states of every recipe the removed engine shipped, by recipe name (the id without 'workflow.'; an instance address names it).
/// </summary>
// why: frozen history from the deleted recipes/ folder (every state with 'kind: terminal'), kept so the retire pass can tell a finished instance from one still open.
internal static class RetiredRecipeEnds
{
    private static readonly Dictionary<string, string[]> _ends = new(StringComparer.Ordinal)
    {
        ["build"] = ["built", "stopped"],
        ["design"] = ["designed", "dropped"],
        ["dev-agent-hold"] = ["done"],
        ["dev-agent-pick"] = ["done"],
        ["dev-child-outputs"] = ["done", "dropped", "unproven"],
        ["dev-child-summary"] = ["done", "dropped"],
        ["dev-desk-round-trip"] = ["done"],
        ["dev-evidence-route"] = ["proven", "unproven"],
        ["dev-incident-hold"] = ["done"],
        ["dev-notify"] = ["done"],
        ["dev-timeout"] = ["done"],
        ["dev-triage-three-state"] = ["done"],
        ["docs-sync"] = ["abandoned", "not-done", "not-needed", "opened"],
        ["evidence"] = ["all-failed", "done", "dropped", "failed"],
        ["evidence-all"] = ["done", "dropped", "failed"],
        ["evidence-api"] = ["failed", "passed", "skipped"],
        ["evidence-db"] = ["failed", "passed", "skipped"],
        ["evidence-desktop"] = ["failed", "passed"],
        ["evidence-other"] = ["none-found", "recorded"],
        ["evidence-ui"] = ["failed", "passed", "skipped"],
        ["hand-test"] = ["broken", "works"],
        ["idea-to-brief"] = ["done", "dropped"],
        ["implement"] = ["built", "stuck"],
        ["investigate"] = ["diagnosed", "unfixed"],
        ["item"] = ["done", "dropped"],
        ["line"] = ["done", "dropped", "stopped"],
        ["phase"] = ["dropped", "merged"],
        ["plan"] = ["dropped", "planned", "redesign"],
        ["pr-babysitter"] = ["closed", "merged", "rework", "stopped"],
        ["prove"] = ["failed", "proven"],
        ["prove-recheck"] = ["broken", "held", "nothing"],
        ["report"] = ["written"],
        ["reproduce"] = ["not-reproduced", "reproduced"],
        ["review-loop"] = ["clean", "stuck", "wrong"],
        ["scoping"] = ["dropped", "scoped"],
        ["ship"] = ["refused", "returned", "shipped"],
        ["step"] = ["done"],
        ["verify"] = ["failed", "passed"],
    };

    // why: for an address whose recipe name isn't in the table: the end states no recipe uses as a live state, so an ambiguous state reads as open.
    private static readonly HashSet<string> _unambiguous = new(StringComparer.Ordinal) { "abandoned", "all-failed", "broken", "built", "clean", "closed", "designed", "diagnosed", "done", "dropped", "failed", "held", "merged", "none-found", "not-done", "not-needed", "not-reproduced", "nothing", "opened", "passed", "planned", "proven", "recorded", "refused", "reproduced", "returned", "rework", "scoped", "shipped", "skipped", "stopped", "stuck", "unfixed", "unproven", "works", "written", "wrong" };

    /// <summary>Whether <paramref name="state"/> ended an instance of <paramref name="address"/>'s recipe.</summary>
    /// <param name="address">The instance address, which names its recipe.</param>
    /// <param name="state">The state the instance's last event entered.</param>
    /// <returns>True for an end state; false for any other state, an unknown one included.</returns>
    public static bool IsEnd(string address, string state)
    {
        var name = RecipeName(address);
        return name is not null && _ends.TryGetValue(name, out var ends) ? Array.IndexOf(ends, state) >= 0 : _unambiguous.Contains(state);
    }

    /// <summary>'T-3/item/1' is 'item'; 'T-3/item/1/step.4' and 'T-3/line/1/phase.p2.1' are 'step' and 'phase'.</summary>
    private static string? RecipeName(string address)
    {
        var parts = address.Split('/');
        var last = parts[^1];
        if (last.Length > 0 && last.All(char.IsAsciiDigit))
        {
            return parts.Length >= 2 ? parts[^2] : null;
        }

        var dot = last.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 ? last[..dot] : null;
    }
}
