using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Which instances exist, by address (port; engine spec rev 8 §5.1, §6.1; engine P4 unit (b)): the attempts of a
/// '(recipe, item)' pair, read straight from the workflow ids so a start never races visibility, and the root a bare
/// item names. The adapter is the Temporal client in Aw.Sindri.Workflows.
/// </summary>
public interface IInstanceDirectory
{
    /// <summary>The latest attempt of <paramref name="recipe"/> on <paramref name="item"/>, and whether it is still live.</summary>
    /// <param name="item">The item.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The latest attempt, or null when none was ever started; SND-API-001 when Temporal doesn't answer.</returns>
    public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken);

    /// <summary>
    /// The instance <paramref name="reference"/> names (spec §6.1, §6.3): its opaque id ('WFI-…', the API's routes), an
    /// address as given, or for a bare item the root of its latest attempt.
    /// </summary>
    /// <param name="reference">'WFI-…', 'T-3/step/1', or a bare 'T-3'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The address; SND-WF-001 when no instance has that id or the item has no instance; SND-WF-006, listing the
    /// candidates, when the item has roots of more than one recipe.
    /// </returns>
    public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken);
}
