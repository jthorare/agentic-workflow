using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The states of the embedded and the approved recipes: what 'workflow.agent.pin' keys of the form 'agent/stateId' are checked against (model-pick spec §8). The profile validator is pattern-only otherwise.</summary>
public interface IRecipeStateCatalog
{
    /// <summary>Every state of every embedded and approved recipe, each once, sorted by recipe then state (ordinal).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The states, or why the recipes couldn't be read.</returns>
    public ValueTask<Result<IReadOnlyList<RecipeStateRef>>> All(CancellationToken cancellationToken);
}
