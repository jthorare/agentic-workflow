using System.Text.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The 'desk:' rows of the instances that wait on the owner at a human-verb wait, read at request time: the folded view has no recipe, so it cannot know a wait
/// state's exit is the owner's click. A wait state with a 'command.&lt;verb&gt;' exit whose effect acts with a human's authority is such a wait (no recipe is named).
/// The open instances are listed once per response, each instance and each pinned recipe read once; any read that fails leaves that one row off, never the list.
/// </summary>
/// <param name="instances">Lists the open instances.</param>
/// <param name="directory">Resolves an instance's address.</param>
/// <param name="reader">The instances' views.</param>
/// <param name="registry">Reads an instance's pinned recipe back by its hash.</param>
/// <param name="catalog">Says which effects act with a human's authority.</param>
/// <param name="views">The 'items' view, for each item's title and repo.</param>
/// <param name="contents">Reads the pull request's status fact.</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="clock">The time the instance's own sentence is served at.</param>
internal sealed class NeedsYouOwnerStops(IWorkflowInstances instances, IInstanceDirectory directory, IInstanceReader reader, RecipeRegistryReader registry, BlockCatalog catalog, IViewReader views, IFactContents contents, ITextScrubber scrubber, TimeProvider clock)
{
    private const string CommandPrefix = "command.";
    private const string PrStatus = "fact:prStatus";
    private const string FallbackLabel = "waiting on you";

    /// <summary>The owner-stop rows <paramref name="caller"/> may see.</summary>
    /// <param name="caller">The caller; a session sees only its repo's and the repo-less ones.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when the instances can't be listed, and a row whose read fails is left off.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CallerContext caller, CancellationToken cancellationToken)
    {
        var open = await Open(cancellationToken).ConfigureAwait(false);
        var recipes = new Dictionary<string, Recipe?>(StringComparer.Ordinal);
        var rows = new List<NeedsYouRow>();
        foreach (var listed in open)
        {
            if (await Row(listed, caller, recipes, cancellationToken).ConfigureAwait(false) is { } row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

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

    private async ValueTask<NeedsYouRow?> Row(InstanceRow listed, CallerContext caller, Dictionary<string, Recipe?> recipes, CancellationToken cancellationToken)
    {
        try
        {
            if (listed.State is null || await View(listed.Address, cancellationToken).ConfigureAwait(false) is not { ItemId: { } itemId } view || view.Question is not null || view.Run is not null || view.Task is not null || view.Outcome is not null)
            {
                return null;
            }

            var recipe = await Recipe(view.Recipe.Hash, recipes, cancellationToken).ConfigureAwait(false);
            if (recipe is null || !recipe.States.TryGetValue(view.State, out var state) || !HumanVerbWait(recipe, view.State, state))
            {
                return null;
            }

            var item = await views.Get<ItemRow>(ViewNames.Items, itemId.Value, cancellationToken).ConfigureAwait(false);
            if (item.IsError || item.Value is not { } row || !ItemScope.VisibleOrRepoless(caller, row.Repo))
            {
                return null;
            }

            var wait = new OwnerStopInput(state.DisplayName?.ToLowerInvariant() ?? FallbackLabel, Reason(view), await Pr(view, cancellationToken).ConfigureAwait(false));
            return NeedsYouRows.ForOwnerStop(row, view.Address, wait, view.Since, scrubber);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

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

    /// <summary>True when <paramref name="state"/> is a wait with a 'command.&lt;verb&gt;' exit carrying an effect that acts with a human's authority.</summary>
    private bool HumanVerbWait(Recipe recipe, string name, RecipeState state) =>
        state.Kind == StateKind.Wait
        && recipe.Transitions.Any(t => t.From.Contains(name, StringComparer.Ordinal) && t.On.StartsWith(CommandPrefix, StringComparison.Ordinal)
            && t.Effects.Any(e => catalog.Find(e.Use) is { Kind: BlockKind.Effect, HumanTrigger: true }));

    // The instance's own sentence for the wait; a view whose status can't be built falls back to the address.
    private string Reason(InstanceView view)
    {
        try
        {
            if (InstanceViews.Status(view, clock.GetUtcNow()).Next is { Length: > 0 } next)
            {
                return next;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // fall through to the plain words
        }

        return $"waiting on you ({view.Address.Value})";
    }

    // The pull request 'fact:prStatus' names (its link and number), or null when the instance holds none or it can't be read.
    private async ValueTask<NeedsYouOpenPr?> Pr(InstanceView view, CancellationToken cancellationToken)
    {
        if (view.FactShas?.GetValueOrDefault(PrStatus) is not { } sha)
        {
            return null;
        }

        try
        {
            var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
            if (bytes.IsError)
            {
                return null;
            }

            using var body = JsonDocument.Parse(bytes.Value);
            var root = body.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String && url.GetString() is { Length: > 0 } link
                && root.TryGetProperty("pr", out var number) && number.TryGetInt32(out var n) && n > 0
                ? new NeedsYouOpenPr(link, n)
                : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
