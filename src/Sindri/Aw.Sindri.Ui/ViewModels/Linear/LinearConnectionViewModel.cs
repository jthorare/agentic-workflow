using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// Settings → Connections → Linear (Linear hub spec §3, §5.1): the read key and the teams to tick, the write key, and the words for each state of them. A key is held in memory
/// for one call and cleared once it is sent; it is never put in a log, a summary or an error. An agent session is refused the connection (SND-AUTH-002) and gets no card.
/// </summary>
internal sealed partial class LinearConnectionViewModel : ObservableObject
{
    private const string Resolves = "resolves";
    private const string Unchecked = "unchecked";
    private const string ScopeUnknown = "scopeUnknown";
    private const string Refused = "refused";
    private const string SessionRefusal = "SND-AUTH-002";

    private static readonly string[] _all =
    [
        nameof(IsAvailable), nameof(Summary), nameof(ReadIsSet), nameof(ShowReadPaste), nameof(ShowReadSet), nameof(ReadAccessText), nameof(CanListTeams), nameof(Teams), nameof(ShowTeams),
        nameof(CanSaveRead), nameof(SaveHint), nameof(ShowWrite), nameof(ShowWritePaste), nameof(ShowWriteSet), nameof(WriteAccessText), nameof(CanCheckWrite), nameof(IsConfirmingRemove),
        nameof(StatusText), nameof(ErrorText), nameof(HasError), nameof(AttentionText), nameof(HasAttention), nameof(HasWaiting), nameof(ShowCancelRead), nameof(CanCancelRead), nameof(ShowCancelWrite), nameof(CanCancelWrite),
    ];

    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private LinearConnection? _connection;
    private string _readKey = string.Empty;
    private string _writeKey = string.Empty;
    private string _error = string.Empty;
    private string _readError = string.Empty;
    private string _waiting = string.Empty;
    private bool _fresh;
    private bool _replacingRead;
    private bool _replacingWrite;
    private bool _lookingUp;
    private bool _saving;
    private bool _checkingWrite;
    private bool _removing;
    private bool _revealPending;
    private int _generation;

    /// <summary>The card over the API.</summary>
    /// <param name="url">The API's base URL, or null while it isn't known.</param>
    /// <param name="api">The API.</param>
    internal LinearConnectionViewModel(Func<Uri?> url, ISindriApi api)
    {
        (_url, _api) = (url, api);
    }

    /// <summary>The card's copy.</summary>
    public LinearConnectionText Text { get; } = LinearConnectionText.Instance;

    /// <summary>False once the API refused this caller the connection (an agent session): the card is not drawn.</summary>
    public bool IsAvailable { get; private set; } = true;

    /// <summary>The line after the card's name: "Not connected", "Reading ENG · read-only" or "Reading ENG · writing ENG".</summary>
    public string Summary => ReadIsSet ? Labels.LinearConnSummary([.. TeamKeys], WriteIsSet) : Labels.LinearConnNotConnected;

    /// <summary>True when a read key is stored and works.</summary>
    public bool ReadIsSet => string.Equals(_connection?.Read.State, Resolves, StringComparison.Ordinal);

    /// <summary>True when the read key box is drawn: no read key is stored (a working one, or one no save has checked yet), or Replace key was pressed.</summary>
    public bool ShowReadPaste => !ReadHasKey || _replacingRead;

    /// <summary>True when the "Read access: connected" line (or its "Not checked yet" form) is drawn.</summary>
    public bool ShowReadSet => ReadHasKey && !_replacingRead;

    /// <summary>"Read access: connected · checked 10:05", or "Read access: Not checked yet. Replace key to check it." for a key no save has checked.</summary>
    public string ReadAccessText => ReadIs(Unchecked) ? Labels.LinearConnNotChecked("Read") : Labels.LinearConnAccess("Read", _connection?.Read.CheckedAt);

    /// <summary>The pasted read key; empty after it is sent.</summary>
    public string ReadKey
    {
        get => _readKey;
        set
        {
            _readKey = value ?? string.Empty;
            Changed();
        }
    }

    /// <summary>True when List my teams can be pressed: a key is typed and no call is running.</summary>
    public bool CanListTeams => _readKey.Length > 0 && !IsBusy;

    /// <summary>The team boxes of the last list.</summary>
    public IReadOnlyList<LinearTeamTickViewModel> Teams { get; private set; } = [];

    /// <summary>True when the team boxes are drawn.</summary>
    public bool ShowTeams => Teams.Count > 0;

    /// <summary>True when Check and save can be pressed for the read key: a key, at least one team ticked, no call running.</summary>
    public bool CanSaveRead => ShowTeams && _readKey.Length > 0 && Teams.Any(t => t.IsTicked) && !IsBusy;

    /// <summary>The line beside Check and save.</summary>
    public string SaveHint => Teams.Any(t => t.IsTicked) ? Labels.LinearConnSaveHint : Labels.LinearConnTickOne;

    /// <summary>True when the write key part is drawn: read access works, or a write key is stored whatever the read key's state (so Remove key stays reachable).</summary>
    public bool ShowWrite => ReadIsSet || WriteHasKey;

    /// <summary>True when the write key box is drawn. A new write key is only asked for while the read key works: checking it reads the teams with that key.</summary>
    public bool ShowWritePaste => WriteHasKey ? _replacingWrite : ReadIsSet;

    /// <summary>True when the "Write access: connected" line (or its "Not checked yet" form) is drawn.</summary>
    public bool ShowWriteSet => WriteHasKey && !_replacingWrite;

    /// <summary>True when Cancel is drawn beside the read key box (Replace key was pressed on a working key).</summary>
    public bool ShowCancelRead => ReadHasKey && _replacingRead;

    /// <summary>True when that Cancel can be pressed: not while a lookup or save runs, whose answer would land anyway.</summary>
    public bool CanCancelRead => ShowCancelRead && !IsBusy;

    /// <summary>True when Cancel is drawn beside the write key box (Replace key was pressed on a stored key).</summary>
    public bool ShowCancelWrite => WriteHasKey && _replacingWrite;

    /// <summary>True when that Cancel can be pressed: not while the key is checked and stored.</summary>
    public bool CanCancelWrite => ShowCancelWrite && !IsBusy;

    /// <summary>"Write access: connected · checked 10:05", or "Write access: Not checked yet. Replace key to check it." for a key no save has checked.</summary>
    public string WriteAccessText => WriteIs(Unchecked) ? Labels.LinearConnNotChecked("Write") : Labels.LinearConnAccess("Write", _connection?.Write.CheckedAt);

    /// <summary>The pasted write key; empty after it is sent.</summary>
    public string WriteKey
    {
        get => _writeKey;
        set
        {
            _writeKey = value ?? string.Empty;
            Changed();
        }
    }

    /// <summary>True when Check and save can be pressed for the write key.</summary>
    public bool CanCheckWrite => _writeKey.Length > 0 && !IsBusy;

    /// <summary>True while Remove key's question is shown.</summary>
    public bool IsConfirmingRemove { get; private set; }

    /// <summary>The progress line ("Looking up your teams…", "Checking…"); empty otherwise. Its region is always in the tree.</summary>
    public string StatusText =>
        _lookingUp ? Labels.LinearConnLookingUp
        : _checkingWrite ? Labels.LinearConnCheckingWrite([.. TeamKeys])
        : _removing ? Labels.LinearConnRemoving
        : _saving ? Labels.LinearConnChecking
        : string.Empty;

    /// <summary>The last refusal or failure; empty otherwise.</summary>
    public string ErrorText => _error.Length > 0 ? _error : _readError;

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The note that needs a look but is not a refusal (the click's own answer, scope unknown, a change waiting for approval, a stored key Linear now refuses); empty otherwise.</summary>
    public string AttentionText =>
        _waiting.Length > 0 ? _waiting
        : WriteIs(ScopeUnknown) ? (_fresh ? Labels.LinearConnScopeUnknown : Labels.LinearConnScopeUnknownRead)
        : _connection?.Pending == true ? Labels.LinearConnPending
        : StaleText;

    /// <summary>True when there is a note to show.</summary>
    public bool HasAttention => AttentionText.Length > 0;

    /// <summary>True when a change waits for the profile approval, so Review changes is drawn.</summary>
    public bool HasWaiting => _waiting.Length > 0 || _connection?.Pending == true;

    private string StaleText =>
        string.Equals(_connection?.Read.State, Refused, StringComparison.Ordinal) ? Labels.LinearConnReadStale
        : WriteIs(Refused) ? Labels.LinearConnWriteStale
        : string.Empty;

    private bool WriteIs(string state) => string.Equals(_connection?.Write.State, state, StringComparison.Ordinal);

    private bool ReadIs(string state) => string.Equals(_connection?.Read.State, state, StringComparison.Ordinal);

    // A key is held (its line is drawn) when it works or when a pointer is set that no save has checked yet.
    private bool ReadHasKey => ReadIsSet || ReadIs(Unchecked);

    private bool WriteHasKey => WriteIsSet || WriteIs(Unchecked);

    private bool IsBusy => _lookingUp || _saving;

    private bool WriteIsSet => _connection is { } c && (string.Equals(c.Write.State, Resolves, StringComparison.Ordinal) || string.Equals(c.Write.State, ScopeUnknown, StringComparison.Ordinal));

    private IEnumerable<string> TeamKeys => (_connection?.Teams ?? []).Select(t => t.Key);

    /// <summary>Raised when the card should be brought into view and given focus, once it has been read.</summary>
    internal event EventHandler? RevealRequested;

    /// <summary>Raised when Review changes is pressed.</summary>
    internal event EventHandler? ReviewRequested;
}
