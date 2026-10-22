using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Work page's states (F3 spec work §6).</summary>
internal static partial class Labels
{
    /// <summary>The box when the queue holds nothing.</summary>
    internal const string NothingInQueue = "Nothing in the queue";

    /// <summary>The box when the filter matches nothing.</summary>
    internal const string NoItemsMatch = "No items match";

    /// <summary>The button that empties the filter.</summary>
    internal const string ClearFilter = "Clear filter";

    /// <summary>The command that adds an item from a terminal.</summary>
    internal const string ItemAddCommand = "sindri item add";

    /// <summary>The group name of the one group the rebuild does not touch.</summary>
    internal const string QueuedGroup = "queued";

    /// <summary>A group's empty line while the views rebuild.</summary>
    /// <param name="cursor">How far the replay is.</param>
    /// <param name="head">Where it is going.</param>
    /// <returns>"Rebuilding views (cursor 8 of 8)…".</returns>
    internal static string RebuildingViews(long cursor, long head) => string.Create(CultureInfo.InvariantCulture, $"Rebuilding views (cursor {cursor} of {head})…");

    /// <summary>The footer when the views are caught up.</summary>
    /// <param name="head">The ledger event the views have reached.</param>
    /// <returns>"Up to date with the ledger (event 214)."</returns>
    internal static string QueueUpToDate(long head) => string.Create(CultureInfo.InvariantCulture, $"Up to date with the ledger (event {head}).");

    /// <summary>The footer when the views are behind.</summary>
    /// <param name="cursor">The event the views have reached.</param>
    /// <param name="head">The ledger's last event.</param>
    /// <returns>"Updated up to event 8 of 9."</returns>
    internal static string QueueBehind(long cursor, long head) => string.Create(CultureInfo.InvariantCulture, $"Updated up to event {cursor} of {head}.");

    /// <summary>The link from an item's open decision to the page where it is answered.</summary>
    internal const string OpenNeedsYou = "Answer in Needs you";

    /// <summary>The repo chip's accessible name.</summary>
    internal const string RepoChipName = "Repo";

    /// <summary>The All segment with its count.</summary>
    /// <param name="count">How many items.</param>
    /// <returns>"All 12".</returns>
    internal static string QueueAllSegment(int count) => string.Create(CultureInfo.InvariantCulture, $"All {count}");

    /// <summary>The Running segment with its count.</summary>
    /// <param name="count">How many items run.</param>
    /// <returns>"Running 3".</returns>
    internal static string QueueRunningSegment(int count) => string.Create(CultureInfo.InvariantCulture, $"Running {count}");

    /// <summary>The Closed fold's row.</summary>
    /// <param name="count">How many closed items were read; null before the first read.</param>
    /// <returns>"Closed" or "Closed (3)".</returns>
    internal static string ClosedHeader(int? count) => count is { } n ? string.Create(CultureInfo.InvariantCulture, $"Closed ({n})") : "Closed";
}
