using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Builds the list row (<see cref="InstanceSummary"/>) from an <see cref="InstanceView"/> (UI screens E-20).</summary>
public static class InstanceSummaries
{
    /// <summary>The longest 'now' text, ellipsis included.</summary>
    public const int NowLength = 80;

    /// <summary>The list row of <paramref name="view"/>: a pure function of the view and the context (no clock, no I/O).</summary>
    /// <param name="view">The instance view; a view that is still starting (no layout yet) reads as a QUEUED row with empty cells.</param>
    /// <param name="context">The time the row is read at, and what only the caller knows (whether a child has not run, the item's title).</param>
    /// <returns>The row: the view's identity, recipe, chip, layout cells, its answer line and its own 'now' sentence.</returns>
    public static InstanceSummary Of(InstanceView view, InstanceSummaryContext context)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(context);
        var chip = StateChip.For(view.EngineOwned, deskOpen: view.Task is not null, view.Outcome, context.NotStarted || view.State == InstanceViews.StartingState);
        return new InstanceSummary(
            view.Address,
            view.Id,
            view.ItemId,
            new InstanceRecipe(view.Recipe.Id, view.Recipe.Hash, view.Recipe.Version, view.Urgency, context.RecipeDisplayName),
            view.State,
            chip,
            view.Since,
            view.EngineOwned,
            SubjectOf(view, context.ItemTitle),
            view.Inputs,
            view.States,
            Cut(RowSentences.Now(view, chip, context.AgentIdle)),
            view.AnswerLine,
            [.. StatusRecords.ActionIds(view), .. Reruns.Of(view)],
            view.StartedBy,
            view.StartedAt,
            context.LineStage,
            RootOf(view.Address),
            RowSentences.IsUnavailable(view) ? WaitingOn.Engine : WaitingOn.Of(chip, deskOpen: view.Task is not null, agentRunOpen: view.AgentRun is not null, ended: view.Outcome is not null),   // a down dependency waits on nobody here: health shows it
            view.EngineOwned is { Kind: EngineOwnedKind.TakenOver } ? view.TakenOverBy : null,
            view.Again);
    }

    /// <summary>
    /// The subject the list row and the detail show: the view's, with an item's title in place of its id when the subject is the
    /// item, the recipe declares no name and the caller knows the title; a subject labelled by nothing but its fact key ('fact:scopeInput') shows the item's title when there is one; an empty rendered name shows the instance address. A view
    /// with no subject yet (a starting instance) is about the item itself: kind 'item', the item's id as the key, its title (else the address) as the label.
    /// </summary>
    /// <param name="view">The instance view.</param>
    /// <param name="itemTitle">The title of the item the instance is on; null when the caller has none.</param>
    /// <returns>The subject.</returns>
    public static SubjectRef SubjectOf(InstanceView view, string? itemTitle)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Subject switch
        {
            null => new("item", view.ItemId?.Value ?? string.Empty, itemTitle is { Length: > 0 } ? itemTitle : view.Address.Value, Href: null),
            { Kind: "item" } item when view.Name is null && itemTitle is { Length: > 0 } && item.Key == view.ItemId?.Value => item with { Label = itemTitle },
            { } keyed when itemTitle is { Length: > 0 } && keyed.Label == keyed.Key && keyed.Key.StartsWith("fact:", StringComparison.Ordinal) => keyed with { Label = itemTitle },
            { Label.Length: 0 } unnamed => unnamed with { Label = view.Address.Value },
            { } subject => subject,
        };
    }

    /// <summary>The root instance's address: the first three segments ('T-3/item/1'); a root's own.</summary>
    /// <param name="address">An instance address.</param>
    /// <returns>The root's address.</returns>
    public static string RootOf(InstanceAddress address) => string.Join('/', address.Value.Split('/').Take(3));

    /// <summary>The text cut to <see cref="NowLength"/> with the ellipsis inside it, never between the two halves of a surrogate pair.</summary>
    private static string Cut(string sentence)
    {
        if (sentence.Length <= NowLength)
        {
            return sentence;
        }

        return string.Concat(SubjectRefs.Cut(sentence, NowLength - 3), "...");
    }
}
