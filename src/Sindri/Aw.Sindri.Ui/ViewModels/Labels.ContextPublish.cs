namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Health page's Context map card (code index spec 6.4). The state is always a word; the sentences under it are Sindri's.</summary>
internal static partial class Labels
{
    /// <summary>The Health page's context map card.</summary>
    internal const string ContextMapCard = "Context map";

    /// <summary>The map is ready and waits for the code index.</summary>
    internal const string MapHeld = "Held for the code index";

    /// <summary>The map's wait ended more than two ticks ago and it is still held.</summary>
    internal const string MapOverdue = "Overdue";

    /// <summary>The map is current and its code answers match the index.</summary>
    internal const string MapPublished = "Published";

    /// <summary>The map went ahead without the index (the first map, waiting turned off, a broken index).</summary>
    internal const string MapPublishedWithoutWaiting = "Published without waiting";

    /// <summary>The map is current, and the index is at another commit than it.</summary>
    internal const string MapPublishedStale = "Published, code answers stale";

    /// <summary>A person promoted the map.</summary>
    internal const string MapPublishedByHand = "Published by hand";

    /// <summary>The card's state in words, from the served state id (the served word when this build does not know the id).</summary>
    /// <param name="state">The state id.</param>
    /// <param name="served">The word Sindri sent with it.</param>
    /// <returns>The words.</returns>
    internal static string MapPublishWord(string state, string served) => state switch
    {
        "held" => MapHeld,
        "overdue" => MapOverdue,
        "published" => MapPublished,
        "published-without-waiting" => MapPublishedWithoutWaiting,
        "published-stale" => MapPublishedStale,
        "published-by-hand" => MapPublishedByHand,
        _ => served,
    };
}
