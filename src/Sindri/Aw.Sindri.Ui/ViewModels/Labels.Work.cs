using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Work page's states (F3 spec work §6).</summary>
internal static partial class Labels
{
    /// <summary>The box when the queue holds nothing.</summary>
    internal const string NothingInQueue = "Nothing in the queue.";

    /// <summary>The box when the filter matches nothing.</summary>
    internal const string NoItemsMatch = "No items match";

    /// <summary>The button that empties the filter.</summary>
    internal const string ClearFilter = "Clear filter";

    /// <summary>The group name of the one group the rebuild does not touch.</summary>
    internal const string QueuedGroup = "queued";

    /// <summary>A group's empty line while the views rebuild.</summary>
    /// <param name="cursor">How far the replay is.</param>
    /// <param name="head">Where it is going.</param>
    /// <returns>"Rebuilding views (cursor 8 of 8)…".</returns>
    internal static string RebuildingViews(long cursor, long head) => string.Create(CultureInfo.InvariantCulture, $"Rebuilding views (cursor {cursor} of {head})…");

    /// <summary>The footer when the views are behind: when they last caught up, never an event number.</summary>
    /// <param name="at">The local time of the last projected event.</param>
    /// <param name="age">How long ago that was.</param>
    /// <returns>"Updated 22:57, 2 min ago".</returns>
    internal static string QueueUpdated(DateTimeOffset at, TimeSpan age) =>
        string.Create(CultureInfo.InvariantCulture, $"Updated {at:HH:mm}, ") + (age.TotalMinutes < 1 ? "just now" : age.TotalMinutes < 60 ? string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes} min ago") : string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours} h ago"));

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

    /// <summary>The fold arrow of an open group: down.</summary>
    internal const string FoldOpen = "▾";

    /// <summary>The fold arrow of a folded group: right.</summary>
    internal const string FoldFolded = "▸";
}
