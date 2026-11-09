using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Memory;

namespace Aw.Sindri.Ui.ViewModels.Memory;

/// <summary>
/// The Memory page: the stores with their fact counts and descriptions, a form to create one (memory_create), and for each store a search,
/// a write form and the forget actions. It reads while it is the page shown and keeps its rows by store name across reads.
/// </summary>
internal sealed class MemoryPageViewModel : ObservableObject, IDisposable
{
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, MemoryStoreRowViewModel> _rows = new(StringComparer.Ordinal);
    private int _reads;
    private CancellationTokenSource? _shown;
    private Uri? _url;

    /// <summary>The page over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="clock">The clock expiry is read against.</param>
    internal MemoryPageViewModel(ISindriApi api, TimeProvider clock)
    {
        (_api, _clock) = (api, clock);
    }

    /// <summary>The page's name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Title => Labels.MemoryPage;

    /// <summary>What the page is for, in one line.</summary>
    public string Purpose => Labels.MemoryPurpose;

    /// <summary>"Reading memory stores…" until the first answer.</summary>
    public string ReadingText => Labels.ReadingMemory;

    /// <summary>What the page says when an answer can't be read.</summary>
    public string UnreadableText => Labels.MemoryUnreadable;

    /// <summary>The stale note's words.</summary>
    public string StaleText => Labels.Stale;

    /// <summary>The retry action's words.</summary>
    public string RetryText => Labels.TryAgain;

    /// <summary>The create form's heading.</summary>
    public string CreateHeading => Labels.MemoryCreateHeading;

    /// <summary>The name field's accessible name.</summary>
    public string NameFieldName => Labels.MemoryNameField;

    /// <summary>The description field's accessible name.</summary>
    public string DescriptionFieldName => Labels.MemoryDescriptionField;

    /// <summary>The create action's words.</summary>
    public string CreateText => Labels.MemoryCreate;

    /// <summary>The sentence for no stores.</summary>
    public string EmptyText => Labels.MemoryEmpty;
#pragma warning restore CA1822, MA0041

    /// <summary>The stores, by name.</summary>
    public ObservableCollection<MemoryStoreRowViewModel> Stores { get; } = [];

    /// <summary>"2 stores · 12 facts"; empty until the first list.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when there are no stores.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>True while the answer can't be read by this build.</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>True while the rows shown are the last good ones (a read failed).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True when a read failed and the retry is offered.</summary>
    public bool ShowRetry { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The catch-up read started by the last show.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>How many lists this page has drawn (a test waits on it).</summary>
    internal int Drawn { get; private set; }

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>Re-reads the list while the page is shown; otherwise it only remembers where the API is.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return IsActive ? Read(url, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>The page was shown or hidden. Becoming the page shown reads at once.</summary>
    /// <param name="shown">True while the Memory page is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        IsActive = shown;
        if (shown && !was && _url is { } url)
        {
            _shown = new CancellationTokenSource();
            CatchUp = CatchUpRead(url, _shown.Token);
        }
        else if (!shown)
        {
            _reads++;   // a read already on its way is not wanted
            Dispose();
        }
    }

    /// <summary>Cancels the read the page started when it was shown.</summary>
    public void Dispose()
    {
        _shown?.Cancel();
        _shown?.Dispose();
        _shown = null;
    }

    /// <summary>Reads again after a failed read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => _url is { } url ? Read(url, cancellationToken) : Task.CompletedTask;

    private async Task CatchUpRead(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // the page was left
        }
        catch (HttpRequestException e)
        {
            Fail(SindriUiErrors.Unreachable.With(e.Message));
        }
    }

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await _api.ListMemories(url, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since, or the page was left
        }

        if (list.IsSuccess)
        {
            Show(list.Value);
            return;
        }

        Fail(list.Error);
    }

    private void Fail(Error error)
    {
        var unreadable = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
        (IsReading, IsUnreadable, IsStale) = (false, unreadable, true);   // the last good rows stay, marked stale
        ShowRetry = !unreadable;
        FailedText = ShowRetry ? Labels.MemoryFailed(error) : string.Empty;
    }

    private void Show(MemoryList list)
    {
        Drawn++;
        (IsReading, IsUnreadable, IsStale, ShowRetry, FailedText) = (false, false, false, false, string.Empty);
        var rows = list.Memories.OrderBy(m => m.Name, StringComparer.Ordinal).Select(Row).ToList();
        var present = new HashSet<string>(list.Memories.Select(m => m.Name), StringComparer.Ordinal);
        foreach (var gone in _rows.Keys.Where(k => !present.Contains(k)).ToList())
        {
            _ = _rows.Remove(gone);
        }

        Patch.To(Stores, rows);
        Summarise();
    }

    private MemoryStoreRowViewModel Row(MemoryView served)
    {
        if (_rows.TryGetValue(served.Name, out var kept))
        {
            kept.Update(served);
            return kept;
        }

        return _rows[served.Name] = new MemoryStoreRowViewModel(served, _api, () => _url, _clock);
    }

    private void Summarise()
    {
        Head = Labels.MemoryHead(Stores.Count, Stores.Sum(s => s.FactCount));
        IsEmpty = Stores.Count == 0;
    }

    // ---- create a store (memory_create) ----

    /// <summary>The new store's name.</summary>
    public string NewName { get; set => Set(ref field, value ?? string.Empty); } = string.Empty;

    /// <summary>The new store's description.</summary>
    public string NewDescription { get; set => Set(ref field, value ?? string.Empty); } = string.Empty;

    /// <summary>False while a create is in flight.</summary>
    public bool CanCreate { get; private set; } = true;

    /// <summary>The sentence for a create that didn't work; empty otherwise.</summary>
    public string CreateError { get; private set { Set(ref field, value); OnChanged(nameof(HasCreateError)); } } = string.Empty;

    /// <summary>True when the last create didn't work.</summary>
    public bool HasCreateError => CreateError.Length > 0;

    /// <summary>Creates the store in the form. It joins the list only after the API answers; the form keeps what was typed on an error.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task CreateStore(CancellationToken cancellationToken)
    {
        var name = NewName.Trim();
        if (name.Length == 0)
        {
            CreateError = Labels.MemoryNameNeeded;
            return;
        }

        if (_url is not { } url)
        {
            CreateError = Labels.MemoryCreateFailed(SindriUiErrors.Unreachable.With("Sindri isn't reachable yet"));
            return;
        }

        var description = NewDescription.Trim();
        var answer = await _api.CreateMemory(url, new CreateMemoryBody(name, description.Length == 0 ? null : description), cancellationToken).ConfigureAwait(true);
        if (!answer.IsSuccess)
        {
            CreateError = Labels.MemoryCreateFailed(answer.Error);
            return;
        }

        (CreateError, NewName, NewDescription) = (string.Empty, string.Empty, string.Empty);
        var rows = Stores.Where(s => !string.Equals(s.Name, answer.Value.Store.Name, StringComparison.Ordinal)).Append(Row(answer.Value.Store)).OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
        Patch.To(Stores, rows);
        Summarise();
    }
}
