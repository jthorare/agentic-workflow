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

    /// <summary>Reads the suggested pages (the map offers them, the profile does not name them) whose node keys are in <paramref name="keys"/>.</summary>
    /// <param name="concept">The concept the section is open on, or null. A null concept matches every page on the server, so <paramref name="keys"/> is what keeps the list to the section's pages.</param>
    /// <param name="keys">The section's page keys; the answer follows their order.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The suggested pages found, or null when the read failed or no API address is known.</returns>
    public Task<IReadOnlyList<PageDraft>?> Suggested(string? concept, IReadOnlyList<string> keys, CancellationToken cancellationToken);

    /// <summary>Writes the pages in one profile write, waits for the job, then approves its hash when <paramref name="approve"/> is set and nothing else waits.</summary>
    /// <param name="save">The pages to set and the page ids to remove.</param>
    /// <param name="ifHash">The live hash the sheet read when it opened.</param>
    /// <param name="approve">True when the button read "Save and approve".</param>
    /// <param name="cancellationToken">Cancels the wait (the write itself is sent once).</param>
    /// <returns>What happened.</returns>
    public Task<PageSaved> Save(PageSave save, string? ifHash, bool approve, CancellationToken cancellationToken);

    /// <summary>Reads a pasted list as one row per page (QUERY /sindri/context/pages/preview): the page each block names, what it matches and the page to send in the save.</summary>
    /// <param name="text">The paste.</param>
    /// <param name="repo">The repo to read, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The preview, or the server's refusal.</returns>
    public Task<PagesPreviewRead> Preview(string text, string? repo, CancellationToken cancellationToken);
}
