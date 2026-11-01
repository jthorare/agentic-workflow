using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The Pages and actions section (pages spec §3.4, §3.5): a concept's pages, or the pages in no concept, each with its actions, in the order the answer gives them
/// (owner-named first, then suggested by number of actions). At most <see cref="Cap"/> rows until Show all.
/// </summary>
internal sealed class PagesSectionViewModel : ObservableObject
{
    /// <summary>The most pages listed before Show all.</summary>
    internal const int Cap = 20;

    private readonly Dictionary<string, string> _savedIds = new(StringComparer.Ordinal);
    private List<PageRowViewModel> _all = [];
    private bool _showingAll;
    private ProfileWaiting? _waiting;

    /// <summary>Raised when a row's button or New page is pressed: the value is the page's node key, or null for a new page.</summary>
    internal event EventHandler<ValueEventArgs<string?>>? SheetRequested;

    /// <summary>Raised when Accept all suggested is pressed: the value is the suggested rows' node keys, in the section's order.</summary>
    internal event EventHandler<ValueEventArgs<IReadOnlyList<string>>>? AcceptAllRequested;

    /// <summary>Gets or sets where the pages are written; null until the host wires it. Without it the section has no Name this page, Edit page or New page.</summary>
    internal IPageWrites? Writes
    {
        get;
        set
        {
            field = value;
            OnChanged(nameof(CanWrite));
            OnChanged(nameof(CanAcceptAll));
        }
    }

    /// <summary>Gets a value indicating whether the section's buttons show.</summary>
    public bool CanWrite => Writes is not null;

    /// <summary>Gets a value indicating whether Accept all suggested shows: writes wired, at least one suggested row.</summary>
    public bool CanAcceptAll => CanWrite && _all.Exists(static r => !r.Owner);

    /// <summary>Gets the Accept all suggested button's words, "Accept all suggested (3)".</summary>
    public string AcceptAllText => Labels.AcceptAllButton(_all.Count(static r => !r.Owner));

    /// <summary>Gets the New page button's words.</summary>
    public string NewPageText { get; } = Labels.ContextPagesNewPage;

    /// <summary>Gets the section's head.</summary>
    public string Head => CanClose ? Labels.ContextPagesNoConceptHead : Labels.ContextPagesHead;

    /// <summary>Gets the pages shown, in the section's order.</summary>
    public IReadOnlyList<PageRowViewModel> Rows => _showingAll ? _all : [.. _all.Take(Cap)];

    /// <summary>Gets a value indicating whether the section has a close button: the pages in no concept lie over the concept cards, a concept's sit in its drill.</summary>
    public bool CanClose { get; private set; }

    /// <summary>Gets the close button's words.</summary>
    public string CloseText { get; } = Labels.ContextPagesClose;

    /// <summary>Gets a value indicating whether a read runs.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets a value indicating whether the read failed.</summary>
    public bool IsFailed { get; private set; }

    /// <summary>Gets the line while the read runs.</summary>
    public string LoadingText { get; } = Labels.ContextPagesLoading;

    /// <summary>Gets the failure line.</summary>
    public string FailedText => CanClose ? Labels.ContextPagesNoConceptFailed : Labels.ContextPagesFailed;

    /// <summary>Gets the Try again button's words.</summary>
    public string RetryText { get; } = Labels.ContextPagesRetry;

    /// <summary>Gets a value indicating whether the empty line shows: an answer came and listed no page.</summary>
    public bool IsEmpty => !IsLoading && !IsFailed && _all.Count == 0;

    /// <summary>Gets the empty line.</summary>
    public string EmptyText => CanClose ? Labels.ContextPagesNoConceptEmpty : Labels.ContextPagesEmpty;

    /// <summary>Gets a value indicating whether Show all shows: more pages than the cap, not all shown yet.</summary>
    public bool HasMore => !_showingAll && _all.Count > Cap;

    /// <summary>Gets the Show all button's words.</summary>
    public string ShowAllText => Labels.ContextPagesShowAll(_all.Count);

    /// <summary>Gets the line the section's status reads out (a polite live region): the loading, failure or empty line, else empty.</summary>
    public string StatusText => IsLoading ? LoadingText : IsFailed ? FailedText : IsEmpty ? EmptyText : string.Empty;

    /// <summary>A read starts: what was listed stays until the answer.</summary>
    /// <param name="noConcept">True for the pages in no concept.</param>
    internal void Loading(bool noConcept = false)
    {
        (CanClose, IsLoading, IsFailed) = (noConcept, true, false);
        Changed();
    }

    /// <summary>The read failed: the failure line with Try again.</summary>
    internal void Failed()
    {
        (IsLoading, IsFailed) = (false, true);
        Changed();
    }

    /// <summary>Lists the pages of an answer; an answer without a pages list lists none.</summary>
    /// <param name="answer">A concept open, or the pages in no concept.</param>
    /// <param name="concept">The opened concept's key, left out of each action's "also in"; null for the pages in no concept.</param>
    /// <param name="noConcept">True for the pages in no concept.</param>
    internal void Show(WholeMapView answer, string? concept = null, bool noConcept = false)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var rows = PageRows.Of(answer, concept);
        _all = Dress(noConcept ? [.. rows.Select(static r => r with { Actions = [.. r.Actions.Select(static a => a with { Opens = false })] })] : rows);
        (CanClose, _showingAll, IsLoading, IsFailed) = (noConcept, false, false, false);
        Changed();
        if (Writes is not null)
        {
            _ = MarkWaiting(CancellationToken.None);
        }
    }

    /// <summary>Asks for a row's sheet: Name this page on a suggested row, Edit page on an owner row.</summary>
    /// <param name="row">The row.</param>
    internal void OpenSheet(PageRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        SheetRequested?.Invoke(this, new ValueEventArgs<string?>(row.Key));
    }

    /// <summary>Asks for the Accept all suggested sheet over the suggested rows listed.</summary>
    internal void AcceptAll() => AcceptAllRequested?.Invoke(this, new ValueEventArgs<IReadOnlyList<string>>([.. _all.Where(static r => !r.Owner).Select(static r => r.Key)]));

    /// <summary>Asks for the New page sheet.</summary>
    internal void NewPage() => SheetRequested?.Invoke(this, new ValueEventArgs<string?>(value: null));

    /// <summary>Gets the names of the pages listed except one, for the sheet's same-name warning.</summary>
    /// <param name="key">The node key of the page the sheet edits, or null for a new page.</param>
    /// <returns>The other pages' names.</returns>
    internal IReadOnlyList<string> NamesExcept(string? key) => [.. _all.Where(r => !string.Equals(r.Key, key, StringComparison.Ordinal)).Select(static r => r.Name)];

    /// <summary>Remembers that a page was written from this row, so its row can wear the Waiting for approval mark once the map is read again.</summary>
    /// <param name="key">The node key the sheet opened from.</param>
    /// <param name="id">The page id it wrote.</param>
    internal void Remember(string key, string id) => _savedIds[key] = id;

    /// <summary>Reads which pages wait for approval and marks their rows. Only the rows it can match are marked: a page this session saved, a page the map says an owner page named, and an owner page whose key carries its id.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the rows are marked.</returns>
    internal async Task MarkWaiting(CancellationToken cancellationToken)
    {
        if (Writes is null)
        {
            return;
        }

        _waiting = await Writes.Waiting(cancellationToken).ConfigureAwait(true);
        _all = Dress(_all);
        Changed();
    }

    // Each row says whether the sheet is wired, and wears Waiting for approval when its page id waits.
    private List<PageRowViewModel> Dress(List<PageRowViewModel> rows) =>
        [.. rows.Select(r => r with { CanWrite = CanWrite, Marks = Waits(r) && !r.Marks.Contains(Labels.ContextPagesWaiting, StringComparer.Ordinal) ? [.. r.Marks, Labels.ContextPagesWaiting] : r.Marks })];

    private bool Waits(PageRowViewModel row) =>
        _waiting is { } waiting && (_savedIds.TryGetValue(row.Key, out var saved) ? saved : row.PageId ?? (row.Owner ? PageRows.OwnerId(row.Key) : null)) is { } id && waiting.Waits(id);

    /// <summary>Lists every page past the cap.</summary>
    internal void ShowAll()
    {
        if (HasMore)
        {
            _showingAll = true;
            Changed();
        }
    }

    /// <summary>Empties the section (the concept closed).</summary>
    internal void Clear()
    {
        (_all, _showingAll, IsLoading, IsFailed) = ([], false, false, false);
        Changed();
    }

    private void Changed()
    {
        foreach (var name in (string[])[nameof(Head), nameof(CanClose), nameof(CanWrite), nameof(CanAcceptAll), nameof(AcceptAllText), nameof(Rows), nameof(IsLoading), nameof(IsFailed), nameof(FailedText), nameof(IsEmpty), nameof(EmptyText), nameof(HasMore), nameof(ShowAllText), nameof(StatusText)])
        {
            OnChanged(name);
        }
    }
}
