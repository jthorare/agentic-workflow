using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Todo panel on Home (todo spec §3.1, §3.4, §3.5).</summary>
internal static partial class Labels
{
    /// <summary>The panel's name: its heading and its region label.</summary>
    internal const string TodoTitle = "Todo";

    /// <summary>The panel when no todo is open.</summary>
    internal const string TodoEmpty = "Nothing on your list. Press N to add one.";

    /// <summary>The Done group's line when nothing was done in a week and nothing removed in a day.</summary>
    internal const string TodoNothingDone = "Nothing done in the last 7 days";

    /// <summary>The group of todos due before today.</summary>
    internal const string TodoGroupOverdue = "Overdue";

    /// <summary>The group of todos due today.</summary>
    internal const string TodoGroupToday = "Today";

    /// <summary>The group of todos due after today.</summary>
    internal const string TodoGroupUpcoming = "Upcoming";

    /// <summary>The group of todos with no due day.</summary>
    internal const string TodoGroupNoDate = "No date";

    /// <summary>The group of todos done in the last 7 days.</summary>
    internal const string TodoGroupDone = "Done";

    /// <summary>The group of todos removed in the last 24 hours.</summary>
    internal const string TodoGroupRemoved = "Removed";

    /// <summary>The toggle that adds the Done group.</summary>
    internal const string TodoShowDone = "Show done";

    /// <summary>The same toggle while the Done group is on.</summary>
    internal const string TodoHideDone = "Hide done";

    /// <summary>The tag on a todo an agent added.</summary>
    internal const string TodoTagAgent = "agent";

    /// <summary>The tag on the owner's todo an agent last changed.</summary>
    internal const string TodoTagEditedByAgent = "edited by an agent";

    /// <summary>The due word of a todo due today.</summary>
    internal const string TodoDueToday = "Today";

    /// <summary>The panel's count line.</summary>
    /// <param name="open">Open todos.</param>
    /// <param name="overdue">Open todos due before today.</param>
    /// <param name="toSortOut">Todos with an open both-places conflict.</param>
    /// <returns>"11 open · 1 overdue · 1 to sort out"; the last two only when above zero.</returns>
    internal static string TodoCountLine(int open, int overdue, int toSortOut) =>
        string.Create(CultureInfo.InvariantCulture, $"{open} open")
        + (overdue > 0 ? string.Create(CultureInfo.InvariantCulture, $" · {overdue} overdue") : string.Empty)
        + (toSortOut > 0 ? string.Create(CultureInfo.InvariantCulture, $" · {toSortOut} to sort out") : string.Empty);

    /// <summary>The control that expands the collapsed panel.</summary>
    /// <param name="open">Open todos.</param>
    /// <returns>"Show all 11".</returns>
    internal static string TodoShowAll(int open) => string.Create(CultureInfo.InvariantCulture, $"Show all {open}");

    /// <summary>The Show done toggle's name for a screen reader.</summary>
    /// <param name="count">Todos the Done and Removed groups would list.</param>
    /// <returns>"Show done todos, 4".</returns>
    internal static string TodoShowDoneName(int count) => string.Create(CultureInfo.InvariantCulture, $"Show done todos, {count}");

    /// <summary>The count of links a row draws no chip for.</summary>
    /// <param name="more">How many.</param>
    /// <returns>"+7".</returns>
    internal static string TodoMoreChips(int more) => string.Create(CultureInfo.InvariantCulture, $"+{more}");

    /// <summary>The due word of an open todo due before today.</summary>
    /// <param name="day">The day, as a short weekday or a month-day.</param>
    /// <returns>"Overdue · Mon".</returns>
    internal static string TodoDueOverdue(string day) => "Overdue · " + day;

    /// <summary>The line when the list could not be read.</summary>
    /// <param name="error">The refusal.</param>
    /// <returns>The sentence.</returns>
    internal static string TodoFailed(Error error) => "Couldn't read your todos just now. " + InstanceRefused(error);

    /// <summary>The first words of a row's sentence for a screen reader.</summary>
    internal const string TodoRowName = "Todo";

    /// <summary>A row's due part when it has no day.</summary>
    internal const string TodoNoDueDate = "no due date";

    /// <summary>A row's state part for an open todo.</summary>
    internal const string TodoNotDone = "not done";

    /// <summary>A row's state part for a ticked todo.</summary>
    internal const string TodoDoneState = "done";

    /// <summary>A row's state part for a removed todo.</summary>
    internal const string TodoRemovedState = "removed";

    /// <summary>A row's part for an agent-added todo.</summary>
    internal const string TodoAddedByAgent = "added by an agent";

    /// <summary>A row's due part.</summary>
    /// <param name="overdue">True for an open todo due before today.</param>
    /// <param name="day">"today", a weekday name or a month and day.</param>
    /// <returns>"due Friday" or "overdue, due Wednesday".</returns>
    internal static string TodoDueName(bool overdue, string day) => (overdue ? "overdue, due " : "due ") + day;

    /// <summary>A row's part for its links.</summary>
    /// <param name="count">How many links.</param>
    /// <returns>"1 link" or "2 links".</returns>
    internal static string TodoLinksName(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "link" : "links")}");
}
