using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the quick-add box for a todo (todo spec §3.2, §3.4, §3.5).</summary>
internal static partial class Labels
{
    /// <summary>The box's accessible name, its field's name and the palette entry.</summary>
    internal const string QuickAddName = "New todo";

    /// <summary>The field's placeholder.</summary>
    internal const string QuickAddPlaceholder = "New todo… (add a day: fri, tomorrow, 11-10)";

    /// <summary>The line under the field when the text has no day.</summary>
    internal const string QuickAddNoDate = "No date";

    /// <summary>The line under the field after Enter on empty text.</summary>
    internal const string QuickAddEmpty = "Type a todo first";

    /// <summary>The reason shown when the save call itself failed (nothing answered).</summary>
    internal const string QuickAddNotAnswering = "Sindri isn't answering";

    /// <summary>The toast after a save without a day.</summary>
    internal const string QuickAddAdded = "Added";

    /// <summary>The toast when N is pressed while Sindri isn't running.</summary>
    internal const string QuickAddStopped = "Sindri isn't running. Start it, then add the todo.";

    /// <summary>The toast after a save with a day.</summary>
    /// <param name="due">The day that was saved.</param>
    /// <returns>"Added. Due Fri".</returns>
    internal static string QuickAddAddedDue(DateOnly due) => "Added. Due " + due.ToString("ddd", CultureInfo.InvariantCulture);

    /// <summary>The toast when an add sent before the box closed was refused.</summary>
    /// <param name="text">The todo's text.</param>
    /// <param name="reason">Why it failed.</param>
    /// <returns>Couldn't add "call Sam": reason.</returns>
    internal static string QuickAddFailed(string text, string reason) => throw new NotSupportedException();

    /// <summary>The line under the field when the text ends in a day.</summary>
    /// <param name="due">The day that will be saved.</param>
    /// <returns>"Due Fri 11-13".</returns>
    internal static string QuickAddDue(DateOnly due) => "Due " + due.ToString("ddd MM-dd", CultureInfo.InvariantCulture);
}
