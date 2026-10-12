using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.ValidateRecipe;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>The 'recipe validate' text output (spec §6.3: rule lines, then the hash). Pure: RecipeCommandTable writes it.</summary>
internal static class RecipeText
{
    /// <summary>
    /// One line per finding (<see cref="RecipeFinding.ToLine"/>): errors first, then information and skipped findings,
    /// labelled 'information: ' and 'skipped: ', so a line that starts with 'RCP-' is always an error. Then
    /// 'hash sha256:…' when the recipe bound.
    /// </summary>
    /// <param name="response">The validation.</param>
    /// <returns>The text, each line ending in '\n'; ASCII when the recipe's own names are.</returns>
    internal static string Validation(ValidateRecipeResponse response)
    {
        var lines = Ordered(response.Findings).Select(f => Label(f.Severity) + f.ToLine() + "\n");
        var hash = response.Hash is { } h ? $"hash {h.Value}\n" : string.Empty;
        return string.Concat(lines) + hash;
    }

    /// <summary>The findings in output order: errors, then information, then skipped; each group in the validator's order.</summary>
    /// <param name="findings">The validation's findings.</param>
    /// <returns>The same findings, reordered.</returns>
    internal static IEnumerable<RecipeFinding> Ordered(IEnumerable<RecipeFinding> findings) => findings.OrderBy(static f => f.Severity);

    private static string Label(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Error => string.Empty,
        FindingSeverity.Information => "information: ",
        FindingSeverity.Skipped => "skipped: ",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Not a finding severity."),
    };
}
