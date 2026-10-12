using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>How the reader's findings name the long-form records and what a value of each type looks like.</summary>
internal static class RecipeTypes
{
    private static readonly Dictionary<Type, string> _what = new()
    {
        [typeof(Recipe)] = "the recipe",
        [typeof(RecipeState)] = "a state",
        [typeof(StepUse)] = "a step",
        [typeof(BlockUse)] = "a block use",
        [typeof(GateUse)] = "a gate",
        [typeof(WaitFor)] = "a waitFor",
        [typeof(AnswerWait)] = "an answer wait",
        [typeof(ChildWait)] = "a child wait",
        [typeof(TimeoutSpec)] = "a timeout",
        [typeof(BudgetSpec)] = "a budget",
        [typeof(RecipeTransition)] = "a transition",
        [typeof(WhenClause)] = "a condition",
        [typeof(EvidenceCheck)] = "an evidence check",
        [typeof(LoopSpec)] = "a loop",
        [typeof(RecipeInput)] = "an input",
        [typeof(RecipeRef)] = "a base",
        [typeof(WithValue)] = "a with value",
    };

    private static readonly Dictionary<Type, string> _expected = new()
    {
        [typeof(int)] = "a whole number",
        [typeof(double)] = "a number",
        [typeof(string)] = "text",
        [typeof(JsonElement)] = "a literal",
        [typeof(RecipeId)] = "a recipe id ('workflow.<name>', kebab-case)",
        [typeof(BlockId)] = "a block id ('<gate|step|effect|evidence|input>.[<area>.]<name>')",
        [typeof(FactKey)] = "a fact key ('fact:<name>' or 'evidence:<kind>@<base|head>')",
        [typeof(EventKind)] = "an event kind ('<area>.<name>')",
        [typeof(OutcomeName)] = "an outcome name (a camelCase word)",
        [typeof(AnswerId)] = "an answer id (a camelCase or kebab-case word)",
        [typeof(RecipeHash)] = "a recipe hash ('sha256:' and 64 lower-case hex digits)",
        [typeof(RecipeDuration)] = "a duration ('10m', '4h', '2d', at least 1m)",
        [typeof(StateKind)] = "active, wait or terminal",
        [typeof(TimeoutAction)] = "park, escalate or retry",
        [typeof(ResourceKind)] = "heavy or slot",
        [typeof(OnParked)] = "propagate or ignore",
        [typeof(InputType)] = "text, file or item",
        [typeof(EvidencePoint)] = "base or head",
        [typeof(EvidenceExpectation)] = "pass or fail",
        [typeof(FactPresence)] = "present or absent",
        [typeof(LoopBound)] = "cap",
        [typeof(NoProgressSignal)] = "findings, emptyDiff or sameFailure",
    };

    /// <summary>The record's name in a finding ('a state').</summary>
    public static string What(Type type) => _what.GetValueOrDefault(type, type.Name);

    /// <summary>What a value of <paramref name="type"/> looks like ('a block id (…)'); a record is 'a mapping'.</summary>
    public static string Expected(Type type)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        return _expected.TryGetValue(plain, out var text) ? text
            : _what.TryGetValue(plain, out var record) ? record + " (a mapping)"
            : "a list or mapping";
    }
}
