using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// Where a served validation reads the profile facts it checks a recipe against (spec §9.1, S1-D11). The registry reads
/// them through this port, so it needn't know the profile namespace, which depends on the event catalog, which depends on
/// the registry.
/// </summary>
public interface IRecipeProfileFacts
{
    /// <summary>The facts of the approved profile; <see cref="RecipeProfileFacts.None"/> when none is approved.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or why the profile couldn't be read.</returns>
    public ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken);
}
