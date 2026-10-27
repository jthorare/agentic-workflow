namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the command palette and the shortcuts sheet (§8, §12.4).</summary>
internal static partial class Labels
{
    /// <summary>The palette's accessible name.</summary>
    internal const string Palette = "Command palette";

    /// <summary>The palette's section of catalog actions.</summary>
    internal const string PaletteActions = "Actions";

    /// <summary>The palette's section of pages.</summary>
    internal const string PaletteGoTo = "Go to";

    /// <summary>A ready entry's status.</summary>
    internal const string Ready = "ready";

    /// <summary>A ready row the app has no sender for: shown, never run (K8).</summary>
    internal const string NotInApp = "coming soon";

    /// <summary>A ready item action that lives on the item's sheet (it needs the version the sheet shows): shown, never run from the palette.</summary>
    internal const string OnItemSheet = "on the item's sheet";

    /// <summary>The palette's line when nothing matches.</summary>
    internal const string NoMatches = "Nothing matches";

    /// <summary>The views banner's live text while the views rebuild (the sentence on screen carries a moving cursor).</summary>
    internal const string ViewsRebuilding = "Refreshing the lists";

    /// <summary>The live text when the stalled or rebuilding banner goes away.</summary>
    internal const string ViewsRecovered = "Lists are up to date";

    /// <summary>The shortcuts sheet's title and accessible name.</summary>
    internal const string Shortcuts = "Keyboard shortcuts";

    /// <summary>The shortcuts sheet's closing hint.</summary>
    internal const string EscCloses = "Esc closes";

    /// <summary>A cli-only entry's status: "Copy: sindri doctor".</summary>
    /// <param name="cli">The catalog's CLI.</param>
    /// <returns>The words.</returns>
    internal static string CopyCli(string cli) => "Copy: " + cli;

    /// <summary>§8's fixed keys and what they do, in the table's order (the `?` sheet).</summary>
    internal static IReadOnlyList<(string Keys, string Action)> FixedKeys { get; } =
    [
        ("⌘K / Ctrl+K", Palette),
        ("G then H N D W Q R C A . S J U", "Go to a page: H or N Home, D Decisions on Home, W Workflows, Q Work, R Workflow types, C Context, A Agents, . or S System, J Jobs, U Settings"),
        ("J / K", "Next / previous row"),
        ("Enter / Esc", "Open the details panel / close the top layer"),
        ("O", "Open the linked page"),
        ("A", "The row's primary action"),
        ("Shift+A", "Mark all as seen"),
        ("X / Shift+X", "Decline / cancel the run"),
        ("T / B", "Take over / hand back"),
        ("Y", "Copy the row's command"),
        ("C / R", "New item / send a request"),
        ("N / ⌘N / Ctrl+N", "Start a workflow (N on Workflows, the chord from any page)"),
        ("/", "Filter this list"),
        ("?", "Shortcuts sheet"),
        ("⌘, / Ctrl+,", Settings),
        ("F6 / Shift+F6", "Next / previous area"),
    ];
}
