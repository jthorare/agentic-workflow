namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Todo panel's row edit and link list (todo spec §3.3, §3.4).</summary>
internal static partial class Labels
{
    /// <summary>Shown on the row once a save has run for 300 ms.</summary>
    internal const string TodoEditSaving = "Saving…";

    /// <summary>The line after "SND-TODO-409": the row was read again, the typed text stays.</summary>
    internal const string TodoEditChanged = "Changed somewhere else. Here's the latest";

    /// <summary>The line after "SND-TODO-423": a both-places conflict is open on the todo.</summary>
    internal const string TodoEditBoth = "Changed in both places. Pick a version first";

    /// <summary>The line when the due field holds a word that is no day.</summary>
    internal const string TodoEditBadDay = "Not a day. Try Fri, tomorrow or 11-10";

    /// <summary>The line when the text field is empty.</summary>
    internal const string TodoEditEmpty = QuickAddEmpty;

    /// <summary>The text field's name for a screen reader.</summary>
    internal const string TodoEditTextName = "Todo text";

    /// <summary>The due field's name for a screen reader.</summary>
    internal const string TodoEditDueName = "Due day";

    /// <summary>The due field's hint while empty.</summary>
    internal const string TodoEditDuePlaceholder = "Due: fri, 11-10";

    /// <summary>The links field's name for a screen reader.</summary>
    internal const string TodoEditLinksName = "Links, separated by commas";

    /// <summary>The links field's hint while empty.</summary>
    internal const string TodoEditLinksPlaceholder = "Links: ENG-12, acme/api#12";

    /// <summary>The link list's name for a screen reader.</summary>
    internal const string TodoLinkListName = "Links of this todo";

    /// <summary>The link list's hint line.</summary>
    internal const string TodoLinkListHint = "J K move · Enter opens · Esc closes";

    /// <summary>The Todo panel rows of the `?` sheet: X, Space, Delete, Enter, O and J, K.</summary>
    internal static IReadOnlyList<(string Keys, string Action)> TodoPanelKeys =>
    [
        ("X / Space", "In the Todo panel: check a todo off, or reopen it"),
        ("Delete", "In the Todo panel: remove a todo (Undo brings it back)"),
        ("Enter", "In the Todo panel: edit a todo in place (Tab for the day and the links, Enter saves, Esc cancels)"),
        ("O", "In the Todo panel: open a todo's link, or pick one from the list"),
        ("J / K", "In the Todo panel: next / previous todo"),
    ];
}
