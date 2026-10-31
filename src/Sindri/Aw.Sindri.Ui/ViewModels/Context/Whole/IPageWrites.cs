using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// How the page sheet reads and writes the owner's pages (pages spec §3.6): the page as the map and the profile hold it, the profile's state, and one write of
/// 'index.pages' through the SetPages job, approved by the app only when it is the only waiting change (the pin rule, concept pins spec §3.6).
/// </summary>
internal interface IPageWrites
{
    /// <summary>Reads the live profile's hash and the key paths that wait for approval.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state, or null when the profile can't be read.</returns>
    public Task<ProfileWaiting?> Waiting(CancellationToken cancellationToken);

    /// <summary>Reads one page of the map (GET /sindri/context/pages) by its node key, owner-named or suggested.</summary>
    /// <param name="key">The page's node key.</param>
    /// <param name="concept">The concept the section is open on, or null for the pages in no concept.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page, or null when the map has no page with that key or the read failed.</returns>
    public Task<PageDraft?> Page(string key, string? concept, CancellationToken cancellationToken);

    /// <summary>Writes the pages in one profile write, waits for the job, then approves its hash when <paramref name="approve"/> is set and nothing else waits.</summary>
    /// <param name="save">The pages to set and the page ids to remove.</param>
    /// <param name="ifHash">The live hash the sheet read when it opened.</param>
    /// <param name="approve">True when the button read "Save and approve".</param>
    /// <param name="cancellationToken">Cancels the wait (the write itself is sent once).</param>
    /// <returns>What happened.</returns>
    public Task<PageSaved> Save(PageSave save, string? ifHash, bool approve, CancellationToken cancellationToken);
}
