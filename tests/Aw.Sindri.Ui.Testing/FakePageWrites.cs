using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The scripted page port: the pages it can read, the profile it reports, the answer the next saves give, and every save it was asked for.</summary>
internal sealed class FakePageWrites : IPageWrites
{
    private readonly Dictionary<string, PageDraft> _pages = new(StringComparer.Ordinal);
    private readonly Queue<PageSaved> _answers = new();
    private readonly Queue<PagesPreviewRead> _previews = new();

    /// <summary>Gets or sets the profile the fake reports; null makes it unreadable.</summary>
    public ProfileWaiting? Profile { get; set; } = new("hash-1", []);

    /// <summary>Gets every save asked for, in order.</summary>
    public List<SavedCall> Saves { get; } = [];

    /// <summary>Gets how many times the profile was read.</summary>
    public int WaitingReads { get; private set; }

    /// <summary>Makes a page readable by its node key.</summary>
    /// <param name="key">The node key.</param>
    /// <param name="page">The page.</param>
    public void Add(string key, PageDraft page) => _pages[key] = page;

    /// <summary>Queues the answer of the next save; once the queue is empty, saves answer Approved.</summary>
    /// <param name="saved">The answer.</param>
    public void Answer(PageSaved saved) => _answers.Enqueue(saved);

    public Task<ProfileWaiting?> Waiting(CancellationToken cancellationToken)
    {
        WaitingReads++;
        return Task.FromResult(Profile);
    }

    public Task<PageDraft?> Page(string key, string? concept, CancellationToken cancellationToken) => Task.FromResult(_pages.GetValueOrDefault(key));

    /// <summary>Gets or sets how many pages the profile already names, as <see cref="OwnerCount"/> reports.</summary>
    public int Owners { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="Suggested"/> answers its pages in the reverse of the keys' order.</summary>
    public bool Reverse { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="Suggested"/> fails (answers null).</summary>
    public bool SuggestedFails { get; set; }

    /// <summary>Gets how many times the suggested pages were read.</summary>
    public int SuggestedReads { get; private set; }

    public Task<int?> OwnerCount(CancellationToken cancellationToken) => Task.FromResult<int?>(Owners);

    public Task<IReadOnlyList<PageDraft>?> Suggested(string? concept, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        SuggestedReads++;
        if (SuggestedFails)
        {
            return Task.FromResult<IReadOnlyList<PageDraft>?>(null);
        }

        List<PageDraft> list = [.. keys.Where(_pages.ContainsKey).Select(k => _pages[k]).Where(static p => !p.Owner)];
        if (Reverse)
        {
            list.Reverse();
        }

        IReadOnlyList<PageDraft>? found = list;
        return Task.FromResult<IReadOnlyList<PageDraft>?>(found);
    }

    /// <summary>Gets or sets the answer every preview gets; null answers a refusal with no code.</summary>
    public PagesPreviewRead? PreviewAnswer { get; set; }

    /// <summary>Queues the answer of the next preview; once the queue is empty, previews answer <see cref="PreviewAnswer"/>.</summary>
    /// <param name="read">The answer.</param>
    public void AnswerPreview(PagesPreviewRead read) => _previews.Enqueue(read);

    /// <summary>Gets every paste asked for, in order.</summary>
    public List<string> Previews { get; } = [];

    /// <summary>Gets or sets a gate every preview waits on before it answers; null answers at once.</summary>
    public TaskCompletionSource? PreviewGate { get; set; }

    public async Task<PagesPreviewRead> Preview(string text, string? repo, CancellationToken cancellationToken)
    {
        Previews.Add(text);
        if (PreviewGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_previews.Count > 0)
        {
            return _previews.Dequeue();
        }

        return PreviewAnswer ?? new PagesPreviewRead(Preview: null, "SND-API-001");
    }

    /// <summary>Gets or sets the pages every search finds.</summary>
    public List<PageMatch> Matches { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether searches fail (answer null).</summary>
    public bool SearchFails { get; set; }

    /// <summary>Gets or sets a gate every search waits on before it answers; null answers at once.</summary>
    public TaskCompletionSource? SearchGate { get; set; }

    /// <summary>Gets every search asked for, in order: repo, text and limit.</summary>
    public List<(string? Repo, string Query, int Limit)> Searches { get; } = [];

    public async Task<IReadOnlyList<PageMatch>?> Search(string? repo, string query, int limit, CancellationToken cancellationToken)
    {
        Searches.Add((repo, query, limit));
        if (SearchGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return SearchFails ? null : Matches;
    }

    /// <summary>Gets or sets the map actions the page's action search reads.</summary>
    public List<PageMatch> ActionMatches { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether action reads fail (answer null).</summary>
    public bool ActionsFail { get; set; }

    /// <summary>Gets or sets a gate every action read waits on before it answers; null answers at once.</summary>
    public TaskCompletionSource? ActionsGate { get; set; }

    /// <summary>Gets the surface of every action read asked for, in order.</summary>
    public List<string> ActionReads { get; } = [];

    public async Task<IReadOnlyList<PageMatch>?> Actions(string surface, CancellationToken cancellationToken)
    {
        ActionReads.Add(surface);
        if (ActionsGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return ActionsFail ? null : ActionMatches;
    }

    public Task<PageSaved> Save(PageSave save, string? ifHash, bool approve, CancellationToken cancellationToken)
    {
        Saves.Add(new SavedCall(save, ifHash, approve));
        return Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : new PageSaved(approve ? PageSavedState.Approved : PageSavedState.Waiting));
    }
}
