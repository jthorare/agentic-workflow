using System.ComponentModel;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>
/// One palette entry (UI spec §8): a catalog row (its control, and its status: "ready" or
/// "Copy: sindri …", "Copied" for 1.5 s after a copy) or a page to go to, each with its key.
/// </summary>
internal sealed class PaletteEntry : ObservableObject
{
    // Control properties that change what the entry shows.
    private static readonly string[] _shown =
    [
        nameof(ControlViewModel.CopyLabel), nameof(ControlViewModel.Phase), nameof(ControlViewModel.Outcome),
        nameof(ControlViewModel.ErrorCode), nameof(ControlViewModel.ErrorDetail), nameof(ControlViewModel.IsAnnouncingSending),
    ];

    private PaletteEntry(string id, string label, string key, bool enabled, ControlViewModel? control, PageId? page, string notRun = Labels.NotInApp)
    {
        (Id, Label, Key, Runs, Control, Page, NotRun) = (id, label, key, enabled, control, page, notRun);
        control?.PropertyChanged += OnControlChanged;
    }

    // What a sendable row that Enter skips says instead of "ready".
    private string NotRun { get; }

    // A row the app has no sender for but opens a form (or dialog) for: it is called ready all the same.
    private bool OpensForm { get; init; }

    /// <summary>Stops listening to its control (the palette rebuilt its entries; the control outlives them).</summary>
    internal void Detach() => Control?.PropertyChanged -= OnControlChanged;

    /// <summary>Its id: the catalog action id, or "go:" and the page.</summary>
    public string Id { get; }

    /// <summary>Its label.</summary>
    public string Label { get; }

    /// <summary>Its key ("G Q", the catalog's shortcut), or empty.</summary>
    public string Key { get; }

    /// <summary>True when it shows a key.</summary>
    public bool HasKey => Key.Length > 0;

    /// <summary>A catalog row's status, or empty for a page.</summary>
    public string Status => Control switch
    {
        null => string.Empty,
        { IsSending: true } => Control.IsAnnouncingSending ? Labels.Sending : string.Empty,   // "Sending…" after 300 ms, as the control's own live region says it; silent before, never "ready"
        { IsAccepted: true } => Control.Outcome,
        { IsFailed: true } => Control.ErrorCode,
        { IsSendable: true } => Runs ? Labels.Ready : NotRun,   // a sendable row is called ready only when Enter does something with it
        _ when OpensForm => Runs ? Labels.Ready : NotRun,
        _ when !Runs => NotRun,   // a copy-only row Enter does nothing with (the item sheet's close actions) says where it lives
        { CopyLabel: Labels.Copied } => Labels.Copied,
        _ => Labels.CopyCli(Control.Cli),
    };

    /// <summary>True while its command is being sent (the spinner; the label stays).</summary>
    public bool IsSending => Control?.IsSending == true;

    /// <summary>True after Sindri refused or failed its command: the code is its status and the detail shows under it.</summary>
    public bool IsFailed => Control?.IsFailed == true;

    /// <summary>The refusal's detail as served, or empty.</summary>
    public string ErrorDetail => Control?.ErrorDetail ?? string.Empty;

    /// <summary>True when Enter does something with it at all, even while it sends.</summary>
    internal bool Runs { get; }

    /// <summary>True when it has a status.</summary>
    public bool HasStatus => Control is not null;

    /// <summary>Its accessible name: its label and its status (an accepted command's outcome alone).</summary>
    public string Name => IsFailed ? $"{Label}, {Status}: {ErrorDetail}"
        : Control is { IsAccepted: true } ? Status   // the outcome names the command already: "Rebuild views: done"
        : Status.Length > 0 ? Label + ", " + Status : Label;

    /// <summary>True for a catalog row.</summary>
    public bool IsAction => Control is not null;

    /// <summary>True when Enter does something (not a command already sending).</summary>
    public bool IsEnabled => Runs && !IsSending;

    /// <summary>True while it is the palette's selection.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>A catalog row's control, or null for a page.</summary>
    public ControlViewModel? Control { get; }

    /// <summary>A page entry's page, or null.</summary>
    public PageId? Page { get; }

    /// <summary>A catalog row's entry: it runs when wired, or for Stop Sindri (which goes to Health), and copies when the app has no form for it.</summary>
    /// <param name="control">The row's control.</param>
    /// <param name="runs">True when Enter does something with it.</param>
    /// <param name="notRun">What a sendable row that Enter skips says instead of "ready".</param>
    /// <param name="opensForm">True when the row opens a form or dialog of the app.</param>
    /// <returns>The entry.</returns>
    internal static PaletteEntry For(ControlViewModel control, bool runs, string notRun = Labels.NotInApp, bool opensForm = false) =>
        new(control.Action, control.Label, control.Shortcut, runs, control, page: null, notRun) { OpensForm = opensForm };

    /// <summary>A page's entry.</summary>
    /// <param name="page">The page.</param>
    /// <param name="key">Its key.</param>
    /// <returns>The entry.</returns>
    internal static PaletteEntry For(PageId page, string key) => new("go:" + page, Labels.Page(page), key, enabled: true, control: null, page);

    /// <summary>True when every character of <paramref name="query"/> appears in order in its label or its id (§8's fuzzy match).</summary>
    /// <param name="query">The query, lower case, without spaces.</param>
    /// <returns>True when it matches.</returns>
    internal bool Matches(string query) => query.Length == 0 || Subsequence(query, Label) || Subsequence(query, Id);

    private static bool Subsequence(string query, string text)
    {
        var at = 0;
        foreach (var c in text)
        {
            if (at < query.Length && char.ToLowerInvariant(c) == query[at])
            {
                at++;
            }
        }

        return at == query.Length;
    }

    private void OnControlChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_shown.Contains(e.PropertyName, StringComparer.Ordinal))
        {
            OnChanged(nameof(Status));
            OnChanged(nameof(Name));
            OnChanged(nameof(IsSending));
            OnChanged(nameof(IsFailed));
            OnChanged(nameof(ErrorDetail));
            OnChanged(nameof(IsEnabled));
        }
    }
}
