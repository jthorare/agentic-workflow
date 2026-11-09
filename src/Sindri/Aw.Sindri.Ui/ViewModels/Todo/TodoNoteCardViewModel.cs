using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// Settings → Connections → Obsidian todo note (todo spec §3.4 "Settings card", §4.5): the vault and note to sync, Check note (writes nothing), Start syncing or Import and start syncing (saves the key,
/// live at once), then the settled line with Change and Stop syncing. Nothing is saved before a click, and Cancel leaves no key.
/// </summary>
internal sealed partial class TodoNoteCardViewModel : ObservableObject
{
    private static readonly string[] _all =
    [
        nameof(IsAvailable), nameof(Stage), nameof(ShowNoVaults), nameof(ShowForm), nameof(ShowResult), nameof(ShowSyncing), nameof(ShowStopAsk), nameof(IsConfirmingStop), nameof(Vaults), nameof(SelectedVault), nameof(NoteText),
        nameof(CanCheck), nameof(ShowCheck), nameof(ResultText), nameof(CountsText), nameof(HasCounts), nameof(CantImportText), nameof(HasCantImport), nameof(OverCapText), nameof(HasOverCap), nameof(OneWayText),
        nameof(HasOneWay), nameof(ShowStart), nameof(ShowStartOneWay), nameof(ShowImport), nameof(ShowCancel), nameof(SyncingText), nameof(StopAskText), nameof(StatusText), nameof(ErrorText), nameof(HasError),
        nameof(IsBusy),
    ];

    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private TodoNoteCheckView? _check;
    private TodoNoteView? _note;
    private string _checkedVault = string.Empty;
    private string _checkedNote = string.Empty;
    private string _selectedVault = string.Empty;
    private string _noteText = "Todo.md";
    private bool _changing;

    /// <summary>The card over the API.</summary>
    /// <param name="url">The API's base URL, or null while it isn't known.</param>
    /// <param name="api">The API.</param>
    /// <param name="clock">The clock; the card words times in its time zone.</param>
    internal TodoNoteCardViewModel(Func<Uri?> url, ISindriApi api, TimeProvider clock)
    {
        (_url, _api, _clock) = (url, api, clock);
    }

    /// <summary>Raised when the Library card should be brought into view (the no-vault sentence's button).</summary>
    internal event EventHandler? LibraryRequested;

    /// <summary>The card's copy.</summary>
    public TodoNoteCardText Text { get; } = TodoNoteCardText.Instance;

    /// <summary>False once a bare 404 showed this Sindri has no todo note routes: the card is not drawn.</summary>
    public bool IsAvailable { get; private set; } = true;

    /// <summary>Where the card stands.</summary>
    public TodoNoteCardStage Stage { get; private set; } = TodoNoteCardStage.Reading;

    /// <summary>The library's vault names.</summary>
    public IReadOnlyList<string> Vaults { get; private set; } = [];

    /// <summary>True while the first read of the setting has no answer yet.</summary>
    public bool ShowReading => Stage == TodoNoteCardStage.Form && false;   // stub

    /// <summary>True when the library has no vault.</summary>
    public bool ShowNoVaults => Stage == TodoNoteCardStage.NoVaults;

    /// <summary>True when the vault and note fields are drawn.</summary>
    public bool ShowForm => Stage is TodoNoteCardStage.Form or TodoNoteCardStage.Checked;

    /// <summary>True when a check's result is drawn.</summary>
    public bool ShowResult => Stage == TodoNoteCardStage.Checked && _check is not null;

    /// <summary>True when the settled line is drawn.</summary>
    public bool ShowSyncing => Stage == TodoNoteCardStage.Syncing;

    /// <summary>True while the stop question is shown.</summary>
    public bool ShowStopAsk => ShowSyncing && IsConfirmingStop;

    /// <summary>True while the stop question is asked.</summary>
    public bool IsConfirmingStop { get; private set; }

    /// <summary>The chosen vault; changing it drops a shown check.</summary>
    public string SelectedVault
    {
        get => _selectedVault;
        set
        {
            if (value is null)
            {
                return;   // the box clears its pick while its list is replaced; the card keeps the vault it had
            }

            _selectedVault = value;
            Edited();
        }
    }

    /// <summary>The typed note path; changing it drops a shown check.</summary>
    public string NoteText
    {
        get => _noteText;
        set
        {
            _noteText = value ?? string.Empty;
            Edited();
        }
    }

    /// <summary>True when a call is running.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>True when Check note can be pressed.</summary>
    public bool CanCheck => !IsBusy && _selectedVault.Length > 0 && _noteText.Trim().Length > 0;

    /// <summary>True when Check note is drawn.</summary>
    public bool ShowCheck => ShowForm;

    /// <summary>The headline of a shown check: the new note, or no open task; empty when the note has tasks to import.</summary>
    public string ResultText =>
        _check is null ? string.Empty
        : !_check.Exists ? Labels.TodoCardNewNote(_checkedNote)
        : _check.Open == 0 ? Labels.TodoCardNoTasks(_checkedNote)
        : string.Empty;

    /// <summary>What an import adds ("Import adds ids to 42 lines in Todo.md and adds your 6 Sindri todos after them").</summary>
    public string CountsText => _check is { Exists: true, Open: > 0 } c && c.CanSwap ? Labels.TodoCardCounts(_checkedNote, c.WillImport, c.SindriOpen) : string.Empty;

    /// <summary>True when the counts line is drawn.</summary>
    public bool HasCounts => CountsText.Length > 0;

    /// <summary>The tasks that can't be imported, with up to five line numbers.</summary>
    public string CantImportText => _check is { CantImport.Count: > 0 } c ? Labels.TodoCardCantImport([.. c.CantImport.Select(l => l.Line)]) : string.Empty;

    /// <summary>True when the can't-import line is drawn.</summary>
    public bool HasCantImport => CantImportText.Length > 0;

    /// <summary>The cap line ("Only 458 will import: 500 open todos is the limit"); empty under the cap.</summary>
    public string OverCapText => _check is { } c && IsOverCap ? Labels.TodoCardOverCap(c.WillImport, c.Cap) : string.Empty;

    /// <summary>True when the cap line is drawn.</summary>
    public bool HasOverCap => OverCapText.Length > 0;

    /// <summary>The one-way sentence; empty when the disk can swap files.</summary>
    public string OneWayText => _check is { CanSwap: false } ? Labels.TodoCardOneWay(_checkedNote) : string.Empty;

    /// <summary>True when the one-way sentence is drawn.</summary>
    public bool HasOneWay => OneWayText.Length > 0;

    /// <summary>True when Start syncing is drawn: a new note, or a note with no open task, on a disk that can swap.</summary>
    public bool ShowStart => ShowResult && _check is { CanSwap: true } c && (!c.Exists || c.Open == 0);

    /// <summary>True when Start syncing (one way) is drawn.</summary>
    public bool ShowStartOneWay => ShowResult && _check is { CanSwap: false };

    /// <summary>True when Import and start syncing is drawn: the note has open tasks and the disk can swap.</summary>
    public bool ShowImport => ShowResult && _check is { Exists: true, Open: > 0, CanSwap: true };

    /// <summary>True when Cancel is drawn: beside a shown check, or while Change edits a running sync.</summary>
    public bool ShowCancel => ShowResult || (Stage == TodoNoteCardStage.Form && _changing);

    /// <summary>"Syncing with Vault/Todo.md · last synced 10:05".</summary>
    public string SyncingText => Stage == TodoNoteCardStage.Syncing ? Labels.TodoCardSyncing(_note?.Vault ?? _checkedVault, _note?.Note ?? _checkedNote, LastSync(), _note?.State ?? "syncing") : string.Empty;

    /// <summary>The question before the sync stops.</summary>
    public string StopAskText => Labels.TodoCardStopAsk(_note?.Note ?? _checkedNote);

    /// <summary>The progress line and the answer to the click (a polite live region, always in the tree); empty otherwise.</summary>
    public string StatusText { get; private set; } = string.Empty;

    /// <summary>The last refusal; empty otherwise.</summary>
    public string ErrorText { get; private set; } = string.Empty;

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasError => ErrorText.Length > 0;

    private bool IsOverCap => _check is { Exists: true } c && c.WillImport < c.Open - c.CantImport.Count;

    private DateTimeOffset? LastSync() => _note?.LastSyncAt is { } at ? TimeZoneInfo.ConvertTime(at, _clock.LocalTimeZone) : null;

    private void Edited()
    {
        if (Stage == TodoNoteCardStage.Checked)
        {
            (_check, Stage, StatusText) = (null, TodoNoteCardStage.Form, string.Empty);   // what was checked is not what is typed any more
        }

        Changed();
    }

    private void Changed()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }

    private void RaiseLibraryRequested() => LibraryRequested?.Invoke(this, EventArgs.Empty);
}
