using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>The trigger rules (spec §3.5, §6.3): RCP-TRIG-001 (the trigger fits its source) and RCP-TRIG-002 (a reserved verb).</summary>
internal sealed partial class GraphRules
{
    private const string CommandPrefix = "command.";

    /// <summary>The verbs 'sindri workflow' uses itself (spec §6.3), which a recipe's command.&lt;verb&gt; can't take.</summary>
    private static readonly FrozenSet<string> _reservedVerbs = FrozenSet.Create(
        StringComparer.Ordinal,
        "answer", "accept", "reject", "resume", "grant", "grant-tokens", "cancel", "takeover", "handback", "complete",
        "start", "approve", "revoke", "override", "freeze", "register", "command");

    /// <summary>Each transition's trigger, checked against the kind of every state it leaves from.</summary>
    private static IEnumerable<RecipeFinding> Triggers(Recipe recipe, RecipeSource source)
    {
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"transitions[{i}].on");
            var finding = Trigger(recipe, recipe.Transitions[i], path, source);
            if (finding is not null)
            {
                yield return finding;
            }
        }
    }

    private static RecipeFinding? Trigger(Recipe recipe, RecipeTransition transition, string path, RecipeSource source)
    {
        var on = transition.On;
        if (on.StartsWith(CommandPrefix, StringComparison.Ordinal))
        {
            var verb = on[CommandPrefix.Length..];
            if (!Verb.IsMatch(verb))
            {
                return source.Finding(
                    "RCP-TRIG-001",
                    path,
                    $"trigger '{on}' has a verb that doesn't match [a-z][a-z-]{{1,31}}",
                    "name the verb with 2 to 32 lowercase letters and dashes, starting with a letter");
            }

            if (_reservedVerbs.Contains(verb))
            {
                return source.Finding("RCP-TRIG-002", path, $"verb '{verb}' is reserved for sindri workflow's own commands", "choose another verb");
            }
        }
        else if (SourceKind(on) is null)
        {
            return source.Finding(
                "RCP-TRIG-001",
                path,
                $"trigger '{on}' isn't step.completed, step.failed, wait.satisfied or command.<verb>",
                "use one of those triggers");
        }

        return Misfit(recipe, transition, path, source);
    }

    /// <summary>The first state in 'from' whose kind the trigger doesn't fit: a terminal state, or the wrong kind for a step or wait trigger.</summary>
    private static RecipeFinding? Misfit(Recipe recipe, RecipeTransition transition, string path, RecipeSource source)
    {
        var fits = SourceKind(transition.On);
        foreach (var from in transition.From)
        {
            if (recipe.States.TryGetValue(from, out var state)
                && (state.Kind == StateKind.Terminal || (fits is { } kind && state.Kind != kind)))
            {
                return source.Finding("RCP-TRIG-001", path, $"trigger '{transition.On}' doesn't fit {KindName(state.Kind)} state '{from}'", MisfitFix(state.Kind));
            }
        }

        return null;
    }

    /// <summary>The kind of state a step or wait trigger leaves from; null for a command (any non-terminal state) or an unknown trigger.</summary>
    private static StateKind? SourceKind(string on) => on switch
    {
        "step.completed" or "step.failed" => StateKind.Active,
        "wait.satisfied" => StateKind.Wait,
        _ => null,
    };

    private static string KindName(StateKind kind) => kind switch
    {
        StateKind.Active => "active",
        StateKind.Wait => "wait",
        StateKind.Terminal => "terminal",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a state kind."),
    };

    private static string MisfitFix(StateKind kind) => kind switch
    {
        StateKind.Active => "from an active state, use step.completed, step.failed or command.<verb>",
        StateKind.Wait => "from a wait state, use wait.satisfied or command.<verb>",
        StateKind.Terminal => "leave terminal states out of from, since nothing leaves one",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a state kind."),
    };

    [GeneratedRegex("^[a-z][a-z-]{1,31}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Verb { get; }
}
