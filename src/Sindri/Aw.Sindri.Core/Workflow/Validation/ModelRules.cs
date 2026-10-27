namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>SND-MODEL-002 (model-pick spec 8, 12): the floor of a state is cheap, standard or strong, never a model id or any other word.</summary>
internal sealed class ModelRules : IRecipeRules
{
    private static readonly string[] _tiers = ["cheap", "standard", "strong"];

    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Floors(document);
    }

    private static IEnumerable<RecipeFinding> Floors(RecipeDocument document)
    {
        var source = document.Source;
        foreach (var (name, state) in document.Recipe.States)
        {
            if (state.Model?.Floor is { } floor && !_tiers.Contains(floor, StringComparer.Ordinal))
            {
                yield return source.Finding("SND-MODEL-002", $"states.{name}.model.floor", $"the floor of state '{name}' is '{floor}', not a tier",
                    "use cheap, standard or strong (a tier, never a model id)");
            }
        }
    }
}
