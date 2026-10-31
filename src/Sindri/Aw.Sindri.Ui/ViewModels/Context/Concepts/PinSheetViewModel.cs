using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>
/// The Pin sheet (concept pins spec §3.6): Pin this, Edit pin and New pin open it with four fields, the name, the definition, the words people use and the features,
/// which go into one index.concepts entry of the profile. The fields fill from the card at once; the counts, the terms and the button's rule arrive after.
/// </summary>
internal sealed partial class PinSheetViewModel : ObservableObject
{
    /// <summary>The most words the sheet fills in from a concept's terms (§3.6).</summary>
    internal const int WordsShown = 20;

    /// <summary>A word that would newly link more records than this starts unticked, likely too broad (§3.6).</summary>
    internal const int TooBroad = 200;

    private const int MaxName = 80;
    private const int MaxDefinition = 280;

    private readonly PinSheetPorts? _ports;
    private IReadOnlyList<PinView> _pins = [];
    private PinSheetOpen? _open;
    private string? _self;
    private string _repo = string.Empty;
    private string? _ifHash;
    private bool _approve;
    private bool _neverApproved;
    private int _folderRules;
    private bool _filling;

    /// <summary>Builds the sheet; without ports it fills and edits but writes nothing.</summary>
    /// <param name="ports">How the sheet reaches the control plane, or null.</param>
    internal PinSheetViewModel(PinSheetPorts? ports = null)
    {
        _ports = ports;
    }

    // Only the paths that read or write the control plane come here, and they are only called with ports.
    private PinSheetPorts Ports => _ports ?? throw new InvalidOperationException("The sheet has no ports.");

    /// <summary>The sheet went away: saved, discarded or closed.</summary>
    internal event EventHandler<PinSheetClosedEventArgs>? Closed;

    /// <summary>Asking to unpin puts focus on Keep pin: the view moves it.</summary>
    internal event EventHandler? KeepPinFocusRequested;

    /// <summary>Keeping the pin puts focus back on Unpin: the view moves it.</summary>
    internal event EventHandler? UnpinFocusRequested;

    /// <summary>Asking to discard puts focus on Keep editing: the view moves it.</summary>
    internal event EventHandler? KeepEditingFocusRequested;

    /// <summary>The sheet opened, or Keep editing: focus goes to the name.</summary>
    internal event EventHandler? NameFocusRequested;

    /// <summary>Pin this, Edit pin or New pin.</summary>
    public PinSheetMode Mode { get; private set; } = PinSheetMode.NewPin;

    /// <summary>The sheet's title.</summary>
    public string Heading => Mode == PinSheetMode.PinThis ? Labels.PinSheetHeadingPin : Mode == PinSheetMode.EditPin ? Labels.PinSheetHeadingEdit : Labels.PinSheetHeadingNew;

    /// <summary>True on Edit pin.</summary>
    public bool IsEdit => Mode == PinSheetMode.EditPin;

    /// <summary>True when the feature picker shows (New pin, Edit pin).</summary>
    public bool HasPicker => Mode != PinSheetMode.PinThis;

    /// <summary>The pin's name.</summary>
    public string Name
    {
        get;
        set
        {
            if (Change(ref field, value))
            {
                Touch();
                OnChanged(nameof(RenameNote));
                OnChanged(nameof(HasRenameNote));
                OnChanged(nameof(CanSubmit));
            }
        }
    } = string.Empty;

    /// <summary>The definition line, shown to people and agents.</summary>
    public string Definition
    {
        get;
        set
        {
            if (Change(ref field, value))
            {
                Touch();
            }
        }
    } = string.Empty;

    /// <summary>The words people use, best first, each with how many records it would newly link.</summary>
    public ObservableCollection<PinWordRow> Words { get; } = [];

    /// <summary>The features the pin holds, by exact key.</summary>
    public ObservableCollection<PinFeatureRow> Features { get; } = [];

    /// <summary>Under the words: why they are empty ("Couldn't read this concept's words just now…"); empty otherwise.</summary>
    public string WordsNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the words have a note.</summary>
    public bool HasWordsNote => WordsNote.Length > 0;

    /// <summary>The word being typed.</summary>
    public string NewWord { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>"Renaming rebuilds the map…" while Edit pin's name differs from the pin's.</summary>
    public string RenameNote => IsEdit && _open?.Pin is { } pin && !string.Equals(Name.Trim(), pin.Name, StringComparison.Ordinal) ? Labels.PinSheetRename : string.Empty;

    /// <summary>True while the rename note shows.</summary>
    public bool HasRenameNote => RenameNote.Length > 0;

    /// <summary>Edit pin: how many paths, dependsOn and owns entries are set outside the app, with "Set outside the app; kept as is."; empty when none.</summary>
    public string FolderRulesNote => _folderRules > 0 ? Labels.PinSheetFolderRules(_folderRules) : string.Empty;

    /// <summary>True when the folder rules note shows.</summary>
    public bool HasFolderRulesNote => _folderRules > 0;

    /// <summary>True while a write is in flight.</summary>
    public bool IsWriting { get; private set; }

    /// <summary>The submit button's word: "Pin and approve", "Pin", "Save and approve", "Save", or "Pinning…" / "Saving…" while the write runs.</summary>
    public string SubmitText => IsWriting && !IsConfirmingUnpin ? Labels.PinSheetSubmitting(IsEdit) : Labels.PinSheetSubmit(IsEdit, _approve);

    /// <summary>The Unpin question's confirm button: "Unpin and approve", "Unpin", or "Unpinning…" while the write runs.</summary>
    public string UnpinConfirmText => IsWriting ? Labels.PinSheetUnpinning : Labels.PinSheetUnpinConfirm(_approve);

    /// <summary>True when the submit button can be pressed: not while writing, and a name is there.</summary>
    public bool CanSubmit => !IsWriting && Name.Trim().Length > 0;

    /// <summary>Under the button: why the last write failed, or that the settings moved; empty otherwise.</summary>
    public string SubmitError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the submit button has an error under it.</summary>
    public bool HasSubmitError => SubmitError.Length > 0;

    /// <summary>"Nothing in the map will match this pin yet; you can still save it." while no word and no feature is ticked.</summary>
    public string EmptyNote => Words.Any(w => w.Ticked) || Features.Any(f => f.Ticked) ? string.Empty : Labels.PinSheetEmpty;

    /// <summary>True when the empty note shows.</summary>
    public bool HasEmptyNote => EmptyNote.Length > 0;

    /// <summary>True when the pin can be unpinned (Edit pin).</summary>
    public bool CanUnpin => IsEdit && _open?.Pin is not null;

    /// <summary>True while the Unpin question is asked.</summary>
    public bool IsConfirmingUnpin { get; private set => Set(ref field, value); }

    /// <summary>"Unpin Billing? It comes back as a suggestion once the map rebuilds."</summary>
    public string UnpinQuestion { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while "Discard your changes?" is asked.</summary>
    public bool IsConfirmingDiscard { get; private set => Set(ref field, value); }

    /// <summary>True when something was typed or ticked since the sheet filled.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Pin this: the sheet filled from a derived concept's card, its terms and its member features.</summary>
    /// <param name="card">The card Pin this was pressed on.</param>
    /// <param name="terms">The concept's terms with their new-link counts (the suggest read's concept answer), best first.</param>
    /// <param name="features">The concept's member features.</param>
    /// <param name="pins">Every pin of the live profile, for the words another pin already owns; none by default.</param>
    /// <returns>The sheet.</returns>
    internal static PinSheetViewModel ForCard(ConceptTileViewModel card, IReadOnlyList<PinSuggestTerm> terms, IReadOnlyList<PinSuggestFeature> features, IReadOnlyList<PinView>? pins = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        var sheet = new PinSheetViewModel { _pins = pins ?? [] };
        sheet.Begin(new PinSheetOpen(string.Empty, card, Pin: null, sheet._pins));
        sheet.FillTerms(terms, features);
        return sheet;
    }

    // The sheet reads as a new edit from here on: setters that fill don't count.
    private void Touch()
    {
        if (!_filling && !IsDirty)
        {
            IsDirty = true;
            OnChanged(nameof(IsDirty));
        }
    }

    private bool Change<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnChanged(name);
        return true;
    }

    // A word or feature tick, a word or feature added: an edit, and the empty note may change.
    private void Edited()
    {
        Touch();
        OnChanged(nameof(EmptyNote));
        OnChanged(nameof(HasEmptyNote));
    }

    private PinWordRow Watched(PinWordRow row)
    {
        row.TickChanged += (_, _) => Edited();
        return row;
    }

    private PinFeatureRow Watched(PinFeatureRow row)
    {
        row.TickChanged += (_, _) => Edited();
        return row;
    }
}
