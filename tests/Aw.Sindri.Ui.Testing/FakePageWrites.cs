using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The scripted page port: the pages it can read, the profile it reports, the answer the next saves give, and every save it was asked for.</summary>
internal sealed class FakePageWrites : IPageWrites
{
    private readonly Dictionary<string, PageDraft> _pages = new(StringComparer.Ordinal);
    private readonly Queue<PageSaved> _answers = new();

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

    /// <summary>Gets or sets the answer every preview gets; null answers a refusal with no code.</summary>
    public PagesPreviewRead? PreviewAnswer { get; set; }

    /// <summary>Gets every paste asked for, in order.</summary>
    public List<string> Previews { get; } = [];

    public Task<PagesPreviewRead> Preview(string text, string? repo, CancellationToken cancellationToken)
    {
        Previews.Add(text);
        return Task.FromResult(PreviewAnswer ?? new PagesPreviewRead(Preview: null, "SND-API-001"));
    }

    public Task<PageSaved> Save(PageSave save, string? ifHash, bool approve, CancellationToken cancellationToken)
    {
        Saves.Add(new SavedCall(save, ifHash, approve));
        return Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : new PageSaved(approve ? PageSavedState.Approved : PageSavedState.Waiting));
    }
}
