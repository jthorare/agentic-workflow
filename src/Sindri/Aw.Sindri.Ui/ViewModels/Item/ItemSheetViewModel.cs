using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>
/// The item detail sheet (UI spec §4.6, §4.9): GET /sindri/items/{id} and its timeline. The header is the API's fields as
/// words; the actions are the item's 'actions' rendered from the catalog; Close item sends the version shown (§4.5). A
/// SND-ITEM-409 re-reads the item and shows the API's detail (§9.1); SND-ITEM-404 shows its detail with Back.
/// </summary>
internal sealed partial class ItemSheetViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock, DecisionSender? decisions = null) : ObservableObject
{
    private const string Conflict = "SND-ITEM-409";
    private const string NotFound = "SND-ITEM-404";

    private readonly Dictionary<string, ControlViewModel> _controls = new(StringComparer.Ordinal);
    private IReadOnlyList<string> _allowed = [];
    private Uri? _url;
    private bool _readProblem;
    private long _version;
    private int _subject;   // bumped when the sheet moves to another item
    private (string Action, Error Error)? _conflict;
    private TaskCompletionSource? _conflictEnded;   // completed when the pending conflict is settled, failed or abandoned: Close waits for it
    private string _conflictAction = string.Empty;   // the 409 line shown, if any: the action it refused and the version it was shown at
    private long _conflictVersion;
    private int _pages;

    /// <summary>Copies the item's id to the clipboard (Details: the id is never up front).</summary>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal async Task CopyId()
    {
        if (ItemId.Length > 0)
        {
            _ = await catalog.Clipboard.SetText(ItemId, CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>The item's decisions and requests (U3' T8).</summary>
    internal ItemNeedsViewModel Needs { get; } = ItemNeedsViewModel.Create(api, decisions ?? new DecisionSender(api), catalog);

    /// <summary>The app's copy for the sheet's headings and buttons.</summary>
    public ItemText Text { get; } = ItemText.Instance;

    /// <summary>The item's id ('ITM-…'), or empty before one is opened.</summary>
    public string ItemId { get; private set { Set(ref field, value); OnChanged(nameof(LoadMoreName)); } } = string.Empty;

    /// <summary>True between Open and the first answer (§4.1: skeleton once).</summary>
    public bool IsLoading { get; private set => Set(ref field, value); }

    /// <summary>True once an item has been read.</summary>
    public bool HasItem { get; private set => Set(ref field, value); }

    /// <summary>The title as served.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The repo, or empty.</summary>
    public string Repo { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The item state as a word.</summary>
    public string StateWord { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"version N": the version Close sends.</summary>
    public string VersionText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>How long since the item entered its state.</summary>
    public string Age { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The engine step, or empty before S2.</summary>
    public string Step { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The item's body as served (scrubbed by the API; plain text), or empty (§4.6).</summary>
    public string Body { get; private set { Set(ref field, value); OnChanged(nameof(HasBody)); } } = string.Empty;

    /// <summary>True when the item has a body.</summary>
    public bool HasBody => Body.Length > 0;

    /// <summary>The tracker record key the item is linked to, or empty (§4.6).</summary>
    public string Link { get; private set { Set(ref field, value); OnChanged(nameof(HasLink)); } } = string.Empty;

    /// <summary>True when the item is linked.</summary>
    public bool HasLink => Link.Length > 0;

    /// <summary>Who created it, as the API names the actor.</summary>
    public string CreatedBy { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The decision it waits on, or empty.</summary>
    public string WaitingOn { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when it waits on a decision.</summary>
    public bool HasWaitingOn => WaitingOn.Length > 0;

    /// <summary>The engine instance's address, or empty before S2.</summary>
    public string Instance { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the API names an instance.</summary>
    public bool HasInstance => Instance.Length > 0;

    /// <summary>True when the item doesn't exist (SND-ITEM-404).</summary>
    public bool IsNotFound { get; private set => Set(ref field, value); }

    /// <summary>True while a refusal the sheet owns is shown (SND-ITEM-404, SND-ITEM-409).</summary>
    public bool HasProblem => ProblemCode.Length > 0;

    /// <summary>The refusal's code (the API's title).</summary>
    public string ProblemCode { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's detail, as served.</summary>
    public string ProblemDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the last read failed: what's shown is the last good answer.</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True while the sheet's polite live region is live (its gate, set before its text changes).</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The live region's text: the problem line the sheet speaks once; resting on the app name, never empty.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The item's actions that the catalog has, in the API's order.</summary>
    public ObservableCollection<ControlViewModel> Actions { get; } = [];

    /// <summary>True when the item has actions to show.</summary>
    public bool HasActions => Actions.Count > 0;

    /// <summary>True when the item has a decision waiting: the sheet shows it as a callout that points to Needs you, where it is answered.</summary>
    public bool HasOpenDecision => Needs.HasDecisions;

    /// <summary>The item the open decision belongs to (the callout's link to Needs you); empty when none is waiting.</summary>
    public string NeedsYouItem => HasOpenDecision ? ItemId : string.Empty;

    /// <summary>The timeline entries read so far, in the API's order.</summary>
    public ObservableCollection<TimelineRow> Timeline { get; } = [];

    /// <summary>One sentence for where the item stands ("Running for 2h 56m"); the sheet's one status line.</summary>
    public string StatusLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the Details expander is open; closed until asked for.</summary>
    public bool DetailsOpen { get; set => Set(ref field, value); }

    /// <summary>The Load more button's accessible name: "Load more timeline for ITM-…".</summary>
    public string LoadMoreName => Labels.LoadMoreFor(ItemId);

    /// <summary>True when the API names a next page.</summary>
    public bool HasMore => Next is not null;

    private long? Next { get; set { field = value; OnChanged(nameof(HasMore)); } }

    private void Show(ItemView item)
    {
        var previous = _version;
        (HasItem, IsNotFound, IsStale, _version) = (true, false, false, item.Version);
        if (ProblemCode == NotFound || _readProblem || ConflictPassed(item))
        {
            _readProblem = false;
            Problem(string.Empty, string.Empty);
            Rest();
        }

        (Title, Repo, StateWord, Step) = (item.Title, item.Repo ?? string.Empty, Labels.ItemState(item.State), item.Step ?? string.Empty);
        (VersionText, Age, CreatedBy) = (Labels.Version(item.Version), Labels.Age(clock.GetUtcNow() - item.Since), item.CreatedBy);
        StatusLine = Labels.StatusFor(StateWord, Age);
        (WaitingOn, Instance) = (item.WaitingOn ?? string.Empty, item.Instance ?? string.Empty);
        (Body, Link) = (item.Body ?? string.Empty, item.Link ?? string.Empty);
        OnChanged(nameof(HasWaitingOn));
        OnChanged(nameof(HasInstance));
        ShowActions(item.Actions);
        Settle(item.Actions);
        CancelQuestions(previous, item.Version);
    }

    // corr-4: the 409 line is the state at the conflict; it goes once the item allows the action again or its version moved on.
    private bool ConflictPassed(ItemView item) =>
        ProblemCode == Conflict && (item.Actions.Contains(_conflictAction, StringComparer.Ordinal) || item.Version != _conflictVersion);

    // corr-9: a question asked about one version is not answered for another; the human is asked again at the new one.
    private void CancelQuestions(long previous, long current)
    {
        if (previous == current)
        {
            return;
        }

        foreach (var asking in _controls.Values.Where(c => c.IsConfirming))
        {
            asking.Keep();
        }
    }

    private void Failed(Error error)
    {
        EndConflict();
        if (error.Code == NotFound)
        {
            var first = ProblemCode != NotFound;
            (IsNotFound, HasItem) = (true, false);
            Problem(error.Code, error.Message);
            ShowActions([]);
            if (first)
            {
                Say($"{error.Code}: {error.Message}");
            }

            return;
        }

        IsStale = HasItem;
        if (!HasItem)
        {
            // Nothing was ever read: say what the API said (until a read succeeds) rather than a skeleton or a blank sheet.
            _readProblem = true;
            Problem(error.Code, error.Message);
        }
    }

    // The pending conflict is over (settled, its re-read failed, or the sheet left the item): whoever waits for it goes on.
    private void EndConflict()
    {
        _conflict = null;
        _ = _conflictEnded?.TrySetResult();
        _conflictEnded = null;
    }

    private void Problem(string code, string detail)
    {
        (ProblemCode, ProblemDetail) = (code, detail);
        OnChanged(nameof(HasProblem));
    }

    private void Clear()
    {
        _controls.Clear();
        Needs.Reset();
        EndConflict();
        _subject++;
        _generation++;
        (_pages, Next, HasItem, IsNotFound, IsStale) = (0, null, false, false, false);
        (Title, Repo, StateWord, VersionText, Age, Step, CreatedBy, WaitingOn, Instance) =
            (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        StatusLine = string.Empty;
        Timeline.Clear();
        Actions.Clear();
        OnChanged(nameof(HasActions));
        (Body, Link) = (string.Empty, string.Empty);
        _allowed = [];
        Problem(string.Empty, string.Empty);
        Rest();
    }
}
