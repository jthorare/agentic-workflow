using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The 'next' rules (spec §3.7): RCP-NEXT-001 (a variable that isn't a recipe variable, or a loop variable naming an
/// undeclared loop) and RCP-NEXT-002 (an approval-tier command, which only a human at a terminal runs; security L6).
/// </summary>
internal sealed partial class GraphRules
{
    private const string NextVariablesFix =
        "use only {item}, {instance}, {state}, {user}, {child}, {loop.<name>}, {loop.<name>.cap} and {budget.tokens.left}";

    /// <summary>The recipe 'next' variables that don't name a loop.</summary>
    private static readonly FrozenSet<string> _nextVariables = FrozenSet.Create(
        StringComparer.Ordinal, "item", "instance", "state", "user", "child", "budget.tokens.left");

    /// <summary>Every finding in every state's 'next', in the order the text holds them.</summary>
    private static IEnumerable<RecipeFinding> NextTexts(Recipe recipe, RecipeSource source) =>
        recipe.States.SelectMany(s => s.Value.Next is { } next ? NextFindings(recipe, s.Key, next, source) : []);

    private static IEnumerable<RecipeFinding> NextFindings(Recipe recipe, string state, string next, RecipeSource source)
    {
        var path = $"states.{state}.next";
        foreach (Match variable in Variable.Matches(next))
        {
            var finding = UnknownVariable(recipe, state, variable.Value, path, source);
            if (finding is not null)
            {
                yield return finding;
            }
        }

        foreach (Match command in ApprovalTierCommand.Matches(next))
        {
            yield return source.Finding(
                "RCP-NEXT-002",
                path,
                $"the next of state '{state}' tells the reader to run '{command.Value}', an approval-tier command",
                "leave it out, since only a human at a terminal runs approval-tier commands");
        }
    }

    private static RecipeFinding? UnknownVariable(Recipe recipe, string state, string variable, string path, RecipeSource source)
    {
        var name = variable[1..^1];
        if (_nextVariables.Contains(name))
        {
            return null;
        }

        var loop = LoopVariable.Match(name);
        if (!loop.Success)
        {
            return source.Finding("RCP-NEXT-001", path, $"the next of state '{state}' uses '{variable}', which isn't a recipe variable", NextVariablesFix);
        }

        var loopName = loop.Groups["loop"].Value;
        return recipe.Loops.ContainsKey(loopName)
            ? null
            : source.Finding(
                "RCP-NEXT-001",
                path,
                $"the next of state '{state}' uses '{variable}', but loop '{loopName}' isn't declared",
                "declare it under loops, or name a declared loop");
    }

    /// <summary>A '{…}' variable reference.</summary>
    [GeneratedRegex("\\{[^{}]*\\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Variable { get; }

    /// <summary>'loop.&lt;name&gt;' or 'loop.&lt;name&gt;.cap'.</summary>
    [GeneratedRegex("^loop\\.(?<loop>[A-Za-z0-9_-]+)(\\.cap)?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LoopVariable { get; }

    /// <summary>The approval-tier (protected) commands of spec §6.2: recipe approve and revoke, profile approve, and answer's --grant, --grant-tokens and --accept.</summary>
    [GeneratedRegex(
        "\\b(recipe\\s+(approve|revoke)|profile\\s+approve)\\b|--(grant-tokens|grant|accept)\\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ApprovalTierCommand { get; }
}
