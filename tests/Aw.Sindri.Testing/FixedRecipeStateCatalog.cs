using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IRecipeStateCatalog"/> over the states a test names, given as 'recipe/state'.</summary>
/// <param name="states">The states, each 'workflow.scoping/questions'.</param>
public sealed class FixedRecipeStateCatalog(params string[] states) : IRecipeStateCatalog
{
    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<RecipeStateRef>>> All(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<RecipeStateRef>>([.. states.Select(static s => s.Split('/')).Select(static p => new RecipeStateRef(p[0], p[1]))
            .OrderBy(static r => r.Recipe, StringComparer.Ordinal).ThenBy(static r => r.State, StringComparer.Ordinal)]));
}
