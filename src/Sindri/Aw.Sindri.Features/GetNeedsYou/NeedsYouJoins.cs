using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The read-time joins of the 'desk:' and 'park:' rows: the stop (which desk state of the instance's pinned recipe it waits in) and the
/// timeout (what the instance status serves, measured at the response). The view's fold has neither, so they are read here, each instance
/// and each recipe once per response. A join that fails leaves its field off that row; the list never fails for it.
/// </summary>
/// <param name="directory">Resolves a row's instance id to its address.</param>
/// <param name="instances">The instances' views.</param>
/// <param name="registry">Reads a registered recipe back by its hash (a read-only door to the registry).</param>
/// <param name="catalog">Says which Steps may run on the desk.</param>
internal sealed class NeedsYouJoins(IInstanceDirectory directory, IInstanceReader instances, RecipeRegistryReader registry, BlockCatalog catalog)
{
    private const string Desk = "desk:";
    private const string Park = "park:";

    /// <summary>Adds the stop and the timeout to the rows that take them.</summary>
    /// <param name="rows">The rows, in order.</param>
    /// <param name="now">The time the response is made at.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows, in the same order.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Apply(IReadOnlyList<NeedsYouRow> rows, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var seen = new Seen();
        var joined = new List<NeedsYouRow>(rows.Count);
        foreach (var row in rows)
        {
            joined.Add(await Join(row, now, seen, cancellationToken).ConfigureAwait(false));
        }

        return joined;
    }

    private async ValueTask<NeedsYouRow> Join(NeedsYouRow row, DateTimeOffset now, Seen seen, CancellationToken cancellationToken)
    {
        var desk = row.Id.Value.StartsWith(Desk, StringComparison.Ordinal);
        if (!desk && !row.Id.Value.StartsWith(Park, StringComparison.Ordinal))
        {
            return row;
        }

        var instance = row.Subject.Id.Value;
        if (!seen.Views.TryGetValue(instance, out var view))
        {
            view = await Read(instance, cancellationToken).ConfigureAwait(false);
            seen.Views[instance] = view;
        }

        if (view is null)
        {
            return row;
        }

        var timeout = Safely(() => InstanceViews.Status(view, now).Timeout);
        if (!desk)
        {
            return row with { Timeout = timeout };
        }

        var key = view.Recipe.Hash.Value;
        if (!seen.Recipes.TryGetValue(key, out var recipe))
        {
            recipe = await Recipe(view.Recipe.Hash, cancellationToken).ConfigureAwait(false);
            seen.Recipes[key] = recipe;
        }

        return row with { Stop = recipe is null ? null : Stop(recipe, view.State), Timeout = timeout };
    }

    private sealed class Seen
    {
        public Dictionary<string, InstanceView?> Views { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Recipe?> Recipes { get; } = new(StringComparer.Ordinal);
    }

    private async ValueTask<InstanceView?> Read(string instance, CancellationToken cancellationToken)
    {
        try
        {
            var address = await directory.Resolve(instance, cancellationToken).ConfigureAwait(false);
            if (address.IsError)
            {
                return null;
            }

            var view = await instances.Get(address.Value, cancellationToken).ConfigureAwait(false);
            return view.IsError ? null : view.Value;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    private async ValueTask<Recipe?> Recipe(RecipeHash hash, CancellationToken cancellationToken)
    {
        try
        {
            var found = await registry.Recipe(hash, cancellationToken).ConfigureAwait(false);
            return found.IsError ? null : found.Value;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>The position of <paramref name="state"/> among the recipe's desk states (an active state whose Step may run on the desk, as the state band marks them), or null when it is none.</summary>
    private NeedsYouStop? Stop(Recipe recipe, string state)
    {
        var desk = recipe.States.Where(s => s.Value is { Kind: StateKind.Active, Step: { } step } && catalog.Find(step.Use) is { DeskCapable: true }).Select(static s => s.Key).ToList();
        var at = desk.FindIndex(s => string.Equals(s, state, StringComparison.Ordinal));
        return at < 0 ? null : new NeedsYouStop(at + 1, desk.Count);
    }

    private static T? Safely<T>(Func<T?> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return default;
        }
    }
}
