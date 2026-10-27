using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>SCP-CHAL-001: the last challenger round reports nothing new and lists every lens and dimension it walked.</summary>
    private static void ChallengerDone(ScopeContext c, RuleSink s)
    {
        if (!s.Need("challenge"))
        {
            return;
        }

        var rounds = ScopeJson.Items(c.Facts["challenge"], "rounds").ToList();
        if (rounds.Count == 0)
        {
            s.Fail(item: null, "the challenger has not run", "Run the challenger; the last round must report nothing new.");
            return;
        }

        var last = rounds[^1];
        var label = string.Create(CultureInfo.InvariantCulture, $"round {rounds.Count}");
        // why: the recipe's challenge loop (cap c.ChallengeCap) is spent at this many rounds; neither a new round nor a redraft can change what the challenger recorded
        var spent = Spent(c);
        if (!string.Equals(ScopeJson.Str(last, "verdict"), "nothingNew", StringComparison.Ordinal)
            || ScopeJson.Items(last, "surfaces").Any() || ScopeJson.Items(last, "behaviours").Any())
        {
            // why: a found round ends the loop only when it is spent; a new round can't clear it, so the owner reads it at the stop
            var message = $"the last challenger round ({label}) found something new";
            if (spent)
            {
                s.Warn(label, message + " and the second opinion is spent", "Read the challenge's findings at the stop; they are open.");
            }
            else
            {
                s.Fail(label, message, "Fold the findings into the map, then run the challenger again until a round finds nothing new.");
            }
        }

        Walked(rounds, label, spent, s);
    }

    private static bool Spent(ScopeContext c) => ScopeJson.Items(c.Facts["challenge"], "rounds").Skip(c.ChallengeCap - 1).Any();

    private static void Walked(List<JsonNode> rounds, string label, bool spent, RuleSink s)
    {
        // why: once the loop is spent the challenger never reruns and the drafter can't fix its record, so a gap in the walk is open at the stop (a warning), not a fail that loops evidence back to the draft
        // why: a round may re-walk only part of the ground, so what counts is what any round walked
        var walked = rounds.SelectMany(static r => ScopeJson.Items(r, "walked")).ToList();
        var lenses = walked.Where(static w => string.Equals(ScopeJson.Str(w, "kind"), "lens", StringComparison.Ordinal)).Select(static w => ScopeJson.Str(w, "name")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var dimensions = walked.Where(static w => string.Equals(ScopeJson.Str(w, "kind"), "dimension", StringComparison.Ordinal)).Select(static w => ScopeJson.Str(w, "name")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var lens in ScopeRules.Lenses.Where(l => !lenses.Contains(l)))
        {
            Gap(s, spent, label, $"the challenger did not walk the {lens} lens");
        }

        foreach (var dimension in ScopeRules.Dimensions.Where(d => !dimensions.Contains(d)))
        {
            Gap(s, spent, label, $"the challenger did not walk the dimension '{dimension}'");
        }
    }

    private static void Gap(RuleSink s, bool spent, string label, string message)
    {
        const string fix = "The challenger lists every lens and dimension it walked.";
        if (spent)
        {
            s.Warn(label, message + " and the second opinion is spent", fix);
        }
        else
        {
            s.Fail(label, message, fix);
        }
    }
}
