using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// Finds the open instances that wait on the owner at a human-verb wait: a wait state of the instance's pinned recipe with a 'command.&lt;verb&gt;' exit whose
/// effect acts with a human's authority. The instances are listed once, each instance and each recipe read once; a read that fails leaves that instance off.
/// </summary>
/// <param name="instances">Lists the open instances.</param>
/// <param name="directory">Resolves an instance's address.</param>
/// <param name="reader">The instances' views.</param>
/// <param name="registry">Reads an instance's pinned recipe back by its hash.</param>
/// <param name="catalog">Says which effects act with a human's authority.</param>
internal sealed class OwnerStopReader(IWorkflowInstances instances, IInstanceDirectory directory, IInstanceReader reader, RecipeRegistryReader registry, BlockCatalog catalog)
{
    private const string CommandPrefix = "command.";

    /// <summary>The instances at a human-verb wait that nothing else holds (no open question, run or desk task, not ended).</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The candidates; empty when the instances can't be listed.</returns>
    public async ValueTask<IReadOnlyList<OwnerStopCandidate>> Candidates(CancellationToken cancellationToken)
    {
        var recipes = new Dictionary<string, Recipe?>(StringComparer.Ordinal);
        var found = new List<OwnerStopCandidate>();
        foreach (var listed in await Open(cancellationToken).ConfigureAwait(false))
        {
            if (listed.State is not null && await Candidate(listed.Address, recipes, cancellationToken).ConfigureAwait(false) is { } candidate)
            {
                found.Add(candidate);
            }
        }

        return found;
    }

    /// <summary>True when <paramref name="state"/> is a wait with a 'command.&lt;verb&gt;' exit carrying an effect that acts with a human's authority.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="name">The state's name.</param>
    /// <param name="state">The state.</param>
    /// <param name="catalog">Says which effects act with a human's authority.</param>
    /// <returns>Whether it is a human-verb wait.</returns>
    internal static bool HumanVerbWait(Recipe recipe, string name, RecipeState state, BlockCatalog catalog) =>
        state.Kind == StateKind.Wait
        && recipe.Transitions.Any(t => t.From.Contains(name, StringComparer.Ordinal) && t.On.StartsWith(CommandPrefix, StringComparison.Ordinal)
            && t.Effects.Any(e => catalog.Find(e.Use) is { Kind: BlockKind.Effect, HumanTrigger: true }));

    private async ValueTask<IReadOnlyList<InstanceRow>> Open(CancellationToken cancellationToken)
    {
        try
        {
            var listed = await instances.List(new InstanceFilter(), ended: false, cancellationToken).ConfigureAwait(false);
            return listed.IsError ? [] : listed.Value;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return [];
        }
    }

    private async ValueTask<OwnerStopCandidate?> Candidate(string address, Dictionary<string, Recipe?> recipes, CancellationToken cancellationToken)
    {
        try
        {
            if (await View(address, cancellationToken).ConfigureAwait(false) is not { } view || Held(view))
            {
                return null;
            }

            var recipe = await Recipe(view.Recipe.Hash, recipes, cancellationToken).ConfigureAwait(false);
            return recipe is not null && recipe.States.TryGetValue(view.State, out var state) && HumanVerbWait(recipe, view.State, state, catalog)
                ? new OwnerStopCandidate(view, state)
                : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    private static bool Held(InstanceView view) => view.ItemId is null || view.Question is not null || view.Run is not null || view.Task is not null || view.Outcome is not null;

    private async ValueTask<Recipe?> Recipe(RecipeHash hash, Dictionary<string, Recipe?> recipes, CancellationToken cancellationToken)
    {
        if (!recipes.TryGetValue(hash.Value, out var recipe))
        {
            var read = await registry.Recipe(hash, cancellationToken).ConfigureAwait(false);
            recipe = read.IsError ? null : read.Value;
            recipes[hash.Value] = recipe;
        }

        return recipe;
    }

    private async ValueTask<InstanceView?> View(string address, CancellationToken cancellationToken)
    {
        var resolved = await directory.Resolve(address, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return null;
        }

        var view = await reader.Get(resolved.Value, cancellationToken).ConfigureAwait(false);
        return view.IsError ? null : view.Value;
    }
}
