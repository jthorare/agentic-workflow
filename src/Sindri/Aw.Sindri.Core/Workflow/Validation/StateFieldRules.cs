using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The per-kind state fields (spec §3.4): which fields an active, wait or terminal state needs and which it can't have
/// (RCP-SCHEMA-001), exactly one form of a waitFor (RCP-SCHEMA-001), a token count of at least 1 (RCP-SCHEMA-002) and a
/// set duration (RCP-DUR-001). One fault gives one finding, and the specific rule wins: a missing timeout is
/// RCP-TIME-001's (the graph rules) and a terminal outcome outside W6's set is RCP-OUTCOME-001's (the wiring rules).
/// </summary>
internal static class StateFieldRules
{
    internal static IEnumerable<RecipeFinding> Check(Recipe recipe, RecipeSource source) =>
        recipe.States.SelectMany(s => CheckState("states." + s.Key, s.Key, s.Value, source));

    internal static RecipeFinding TooFewTokens(RecipeSource source, string path, int tokens) =>
        source.Finding("RCP-SCHEMA-002", path, string.Create(CultureInfo.InvariantCulture, $"a budget of {tokens} tokens allows nothing"),
            "set tokens to at least 1 (for example 200k)");

    private static IEnumerable<RecipeFinding> CheckState(string path, string name, RecipeState state, RecipeSource source)
    {
        foreach (var (field, present) in Fields(state))
        {
            var need = Need(state.Kind, field);
            // why: a missing timeout is RCP-TIME-001's, so it isn't reported twice
            if (need == Presence.Required && !present && field != "timeout")
            {
                yield return source.Finding("RCP-SCHEMA-001", $"{path}.{field}", $"{Kind(state.Kind)} state '{name}' has no '{field}'", Needs(state.Kind));
            }
            else if (need == Presence.Forbidden && present)
            {
                yield return source.Finding("RCP-SCHEMA-001", $"{path}.{field}", $"{Kind(state.Kind)} state '{name}' can't have '{field}'",
                    $"remove '{field}'; {Needs(state.Kind)}");
            }
        }

        foreach (var finding in Values(path, name, state, source))
        {
            yield return finding;
        }
    }

    private static IEnumerable<RecipeFinding> Values(string path, string name, RecipeState state, RecipeSource source)
    {
        if (state is { Kind: StateKind.Wait, WaitFor: { } wait } && Forms(wait) != 1)
        {
            yield return source.Finding("RCP-SCHEMA-001", path + ".waitFor",
                string.Create(CultureInfo.InvariantCulture, $"waitFor of state '{name}' names {Forms(wait)} of event, answer, resource and child"),
                "name exactly one: { event }, { answer }, { resource } or { child }");
        }

        if (state.Budget is { Tokens: < 1 } budget)
        {
            yield return TooFewTokens(source, path + ".budget.tokens", budget.Tokens);
        }

        if (state.Timeout is { } timeout && !timeout.After.IsInitialized())
        {
            yield return source.Finding("RCP-DUR-001", path + ".timeout.after", $"the timeout of state '{name}' has no duration",
                "write a whole number and m, h or d, at least 1m ('10m', '4h', '2d')");
        }
    }

    private static int Forms(WaitFor wait) =>
        (wait.Event is null ? 0 : 1) + (wait.Answer is null ? 0 : 1) + (wait.Resource is null ? 0 : 1) + (wait.Child is null ? 0 : 1);

    private static (string Field, bool Present)[] Fields(RecipeState s) =>
    [
        ("step", s.Step is not null),
        ("waitFor", s.WaitFor is not null),
        ("timeout", s.Timeout is not null),
        ("loop", s.Loop is not null),
        ("budget", s.Budget is not null),
        ("next", s.Next is not null),
        ("outcome", s.Outcome is not null),
    ];

    /// <summary>The table in spec §3.4.</summary>
    private static Presence Need(StateKind kind, string field) => (kind, field) switch
    {
        (StateKind.Active, "step" or "timeout" or "next") => Presence.Required,
        (StateKind.Active, "loop" or "budget") => Presence.Optional,
        (StateKind.Wait, "waitFor" or "timeout" or "next") => Presence.Required,
        (StateKind.Wait, "loop") => Presence.Optional,
        (StateKind.Terminal, "outcome") => Presence.Required,
        (StateKind.Terminal, "next") => Presence.Optional,
        _ => Presence.Forbidden,
    };

    private static string Kind(StateKind kind) => kind switch
    {
        StateKind.Active => "active",
        StateKind.Wait => "wait",
        StateKind.Terminal => "terminal",
        _ => "terminal",
    };

    private static string Needs(StateKind kind) => kind switch
    {
        StateKind.Active => "an active state has step, timeout and next, and may have loop and budget",
        StateKind.Wait => "a wait state has waitFor, timeout and next, and may have loop",
        StateKind.Terminal => "a terminal state has outcome, and may have next",
        _ => "a terminal state has outcome, and may have next",
    };

    private enum Presence
    {
        Required,
        Optional,
        Forbidden,
    }
}
