using System.ComponentModel;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>
/// The command palette (UI spec §8). Sections: <b>Actions</b>, every catalog row not in Sindri, ready first, then
/// pending (shown, never run), then cli-only (copied); <b>Go to</b>, the pages with their keys; <b>Sindri</b>, Rebuild
/// views, Stop Sindri (which goes to Health, where it lives: §4.8; its confirm is U4) and Health. Typing filters by a
/// fuzzy match on labels and ids; ↑/↓ move between entries that do something; Enter runs one and Esc closes. U1 has no
/// selected row, so Actions lists the whole catalog. It decides nothing about an action: the catalog says what runs.
/// </summary>
internal sealed partial class CommandPaletteViewModel : ObservableObject
{
    // The Sindri section's catalog rows, in its order.
    private static readonly string[] _sindri = ["RebuildViews", "StopProcess"];

    // §8's G-chords (and ⌘, for Settings), by page.
    private static readonly (PageId Page, string Key)[] _pages =
    [
        (PageId.Home, "G H"), (PageId.Workflows, "G W"), (PageId.Work, "G Q"), (PageId.Recipes, "G R"), (PageId.Context, "G C"), (PageId.Agents, "G A"),
        (PageId.System, "G ."), (PageId.Jobs, "G J"), (PageId.Settings, "⌘,"),
    ];

    private readonly CatalogViewModel _catalog;
    private readonly Action<PageId> _goTo;
    private readonly Action<string> _remember;
    private IReadOnlyList<PaletteSection>? _all;

    /// <summary>The palette over the catalog.</summary>
    /// <param name="catalog">The catalog (read on connect).</param>
    /// <param name="goTo">Shows a page.</param>
    /// <param name="remember">Records an entry run (the paletteHistory preference).</param>
    /// <param name="outcomes">The shell's region for a run whose palette closed before its outcome; null hands such a run back to its control.</param>
    /// <param name="createItem">Opens the Create item form (the CreateItem entry runs it); null leaves that entry unrunnable.</param>
    /// <param name="startWorkflow">Opens the Start form (the StartInstance entry runs it); null leaves that entry unrunnable.</param>
    internal CommandPaletteViewModel(CatalogViewModel catalog, Action<PageId> goTo, Action<string> remember, CommandOutcomeRegion? outcomes = null, Action? createItem = null, Action? startWorkflow = null)
    {
        (_catalog, _goTo, _remember, Outcomes, CreateItem, StartWorkflow) = (catalog, goTo, remember, outcomes, createItem, startWorkflow);
        _catalog.PropertyChanged += OnCatalogChanged;
    }

    /// <summary>Opens the Create item form, or null.</summary>
    internal Action? CreateItem { get; }

    /// <summary>Opens the Start form, or null.</summary>
    internal Action? StartWorkflow { get; }

    /// <summary>The shell's region for a run whose palette closed before its outcome, or null.</summary>
    internal CommandOutcomeRegion? Outcomes { get; }

    /// <summary>The palette's accessible name.</summary>
    public string Title { get; } = Labels.Palette;

    /// <summary>The query field's placeholder.</summary>
    public string Placeholder { get; } = Labels.Search;

    /// <summary>The line shown when nothing matches.</summary>
    public string NoMatches { get; } = Labels.NoMatches;

    /// <summary>What Enter does with a ready command: the one place the palette's run policy is decided (U1 finding F2).</summary>
    internal PaletteRunPolicy RunPolicy { get; set; } = PaletteRunPolicy.StayOpen;

    /// <summary>
    /// True when the list should scroll to keep the selection in view (after ↑/↓); false after it opens or refilters,
    /// when the list starts at the top with its first section heading in view (U1 finding F1).
    /// </summary>
    public bool FollowsSelection { get; private set; }

    /// <summary>True while open.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The typed query; setting it filters the entries.</summary>
    public string Query
    {
        get;
        set
        {
            Set(ref field, value ?? string.Empty);
            Filter();
        }
    } = string.Empty;

    /// <summary>The sections with matching entries, in order (a section with none is left out).</summary>
    public IReadOnlyList<PaletteSection> Sections { get; private set => Set(ref field, value); } = [];

    /// <summary>Every matching entry, in order.</summary>
    public IReadOnlyList<PaletteEntry> Entries { get; private set => Set(ref field, value); } = [];

    /// <summary>The polite live region's text: never empty (Avalonia #22312 crashes VoiceOver on an empty live region).</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.Palette;

    /// <summary>The live region's gate: true except for the moment it rests on a neutral text so that a repeated announcement is a change (flag first, then the text).</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); } = true;

    /// <summary>True when nothing matches the query.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>The selected entry (Enter runs it), or null when nothing that runs matches.</summary>
    public PaletteEntry? Selected
    {
        get;
        private set
        {
            field?.IsSelected = false;
            Set(ref field, value);
            field?.IsSelected = true;
        }
    }

    /// <summary>Opens it with an empty query, the first entry that runs selected.</summary>
    internal void Open()
    {
        _all ??= Build();
        Query = string.Empty;
        IsOpen = true;
    }

    /// <summary>Closes it (Esc) and clears the query.</summary>
    internal void Close()
    {
        IsOpen = false;
        HandBack();   // a run still sending is then spoken by the shell's region, or by its own control when its view is on show
        Query = string.Empty;
    }

    /// <summary>Moves the selection over the entries that run (↑ −1, ↓ +1), stopping at either end.</summary>
    /// <param name="by">How far.</param>
    internal void Move(int by)
    {
        var runs = Stops();
        if (runs.Count == 0)
        {
            return;
        }

        var at = Selected is { } selected ? runs.IndexOf(selected) : -1;
        FollowsSelection = true;
        Selected = runs[Math.Clamp(at + by, 0, runs.Count - 1)];
        Announce();
    }

    /// <summary>
    /// Runs an entry (Enter, or a click): a page is shown, Stop Sindri goes to Health, a ready row is sent once as
    /// <see cref="RunPolicy"/> says (by default the palette stays open and the entry shows §4.1's lifecycle; Esc still
    /// closes it and the command keeps running), and a cli-only row is copied ("Copied", the palette stays open). A
    /// pending entry, or one already sending, does nothing. What ran is remembered.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Cancels a send or a copy.</param>
    /// <returns>A task that completes when it ran.</returns>
    internal async Task Run(PaletteEntry? entry, CancellationToken cancellationToken)
    {
        if (entry is not { IsEnabled: true })
        {
            return;
        }

        _remember(entry.Id);
        switch (entry)
        {
            case { Page: { } page }:
                // The page changes first, so the region the close hands focus back to already carries the new page's name.
                _goTo(page);
                Close();
                break;
            case { Control.IsCliOnly: true }:
                await Copy(entry, entry.Control, cancellationToken).ConfigureAwait(true);
                break;
            case { Control.Action: "StopProcess" }:
                Close();
                _goTo(PageId.System);
                break;
            case { Control.Action: "CreateItem" }:
                CreateItem?.Invoke();   // the form's own control sends it; it opens first, so the window hands the palette's opener on to it
                Close();
                break;
            case { Control.Action: "StartInstance" }:
                StartWorkflow?.Invoke();   // the form sends it; it opens first, so the window hands the palette's opener on to it
                Close();
                break;
            default:
                // why: an enabled entry without a page has a control
                await Send(entry, entry.Control!, cancellationToken).ConfigureAwait(true);
                break;
        }
    }

    private List<PaletteSection> Build()
    {
        var actions = _catalog.Controls.Where(c => !_sindri.Contains(c.Action, StringComparer.Ordinal)).OrderBy(c => c.Kind)
            .Select(c => PaletteEntry.For(c, Runs(c), NotRun(c)));
        var sindri = _sindri.Select(_catalog.For).OfType<ControlViewModel>().Select(c => PaletteEntry.For(c, Runs(c), NotRun(c)))
            .Append(PaletteEntry.For(PageId.System, "G ."));
        return
        [
            new(Labels.PaletteActions, [.. actions]),
            new(Labels.PaletteGoTo, [.. _pages.Select(p => PaletteEntry.For(p.Page, p.Key))]),
            new(Labels.Sindri, [.. sindri]),
        ];
    }

    // Enter does something with a ready row that has a sender, with Stop Sindri (it goes to Health) and with a cli-only row it can copy.
    // Never sends a destructive one: its in-app confirm is on its own control (Stop Sindri's is on Health, where Enter goes).
    // Create item opens its form (when the shell has one); the item sheet's close actions need the version the sheet shows.
    private bool Runs(ControlViewModel control) =>
        (control.IsSendable && !control.IsDestructive) || (control.IsReady && control.Action == "StopProcess")
        || OpensForm(control) || (control.IsCliOnly && control.CanCopy);

    // Create item and Start workflow open their forms (when the shell has them).
    private bool OpensForm(ControlViewModel control) =>
        control.IsReady && ((control.Action == "CreateItem" && CreateItem is not null) || (control.Action == "StartInstance" && StartWorkflow is not null));

    // What a ready row that Enter skips says instead of "ready": where it lives, or that the app has no sender for it.
    private static string NotRun(ControlViewModel control) =>
        ItemSheetViewModel.IsSheetAction(control.Action) ? Labels.OnItemSheet : Labels.NotInApp;

    // The entries ↑/↓ stop at: those that run now, and the selection even while it sends (it is disabled then, but it is
    // where the user is: arrowing off it goes to its neighbour).
    private List<PaletteEntry> Stops() => [.. Entries.Where(e => e.IsEnabled || e == Selected)];

    // The entries the live region counts: every one that runs, a sending one too, so a position and its total do not
    // change when the user moves off an entry that started sending.
    private List<PaletteEntry> Counted() => [.. Entries.Where(e => e.Runs)];

    private void Filter()
    {
        _all ??= Build();
        var query = new string([.. Query.Where(c => !char.IsWhiteSpace(c)).Select(char.ToLowerInvariant)]);
        Sections = [.. _all.Select(s => s with { Entries = [.. s.Entries.Where(e => e.Matches(query))] }).Where(s => s.Entries.Count > 0)];
        Entries = [.. Sections.SelectMany(s => s.Entries)];
        OnChanged(nameof(IsEmpty));
        FollowsSelection = false;
        Selected = Entries.FirstOrDefault(e => e.IsEnabled);
        Announce();
    }

    // The live region says the selected entry (its name carries the status) and where it is among the entries that run;
    // "Nothing matches" when empty; never "" (Avalonia #22312 crashes VoiceOver on an empty live region).
    private void Announce() => Announcement = Describe();

    private string Describe()
    {
        var runs = Counted();
        return Entries.Count == 0 ? Labels.NoMatches
            : Selected is { } selected ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{selected.Name}, {runs.IndexOf(selected) + 1} of {runs.Count}")
            : Labels.Palette;
    }

    // A catalog read while it is open (a reconnect) replaces its entries.
    private void OnCatalogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CatalogViewModel.Controls))
        {
            return;
        }

        Selected = null;
        ReleaseAll();
        _all?.SelectMany(s => s.Entries).ToList().ForEach(entry => entry.Detach());
        _all = null;
        if (IsOpen)
        {
            Filter();
        }
    }
}
