using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The app's side of <see cref="IPageWrites"/>: reads the map's pages through the API and writes pages with the SetPages job through the shared <see cref="ProfileWriteFlow"/>.</summary>
/// <param name="flow">The write-then-approve flow.</param>
/// <param name="api">Sindri's API.</param>
/// <param name="apiUrl">The control plane's API address now, or null when none is known.</param>
internal sealed class PageWrites(ProfileWriteFlow flow, ISindriApi api, Func<Uri?> apiUrl) : IPageWrites
{
    private const int PageSize = 200;
    private const int MaxReads = 5;

    /// <inheritdoc />
    public Task<ProfileWaiting?> Waiting(CancellationToken cancellationToken) => flow.Waiting(cancellationToken);

    /// <inheritdoc />
    public async Task<PageDraft?> Page(string key, string? concept, CancellationToken cancellationToken)
    {
        if (apiUrl() is not { } url)
        {
            return null;
        }

        for (var read = 0; read < MaxReads; read++)
        {
            var answer = await api.ContextPages(url, new PagesQuery(Concept: concept, Limit: PageSize, Offset: read * PageSize), cancellationToken).ConfigureAwait(true);
            if (!answer.IsSuccess)
            {
                return null;
            }

            if (answer.Value.Pages.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal)) is { } found)
            {
                return Draft(found);
            }

            if (!answer.Value.Truncated)
            {
                return null;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<PageSaved> Save(PageSave save, string? ifHash, bool approve, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(save);
        var body = new PagesSetBody([.. save.Set.Select(Body)], [.. save.Remove], IfHash: ifHash);
        List<string> ids = [.. save.Set.Select(p => p.Id), .. save.Remove];
        var written = await flow.Run((url, token) => api.SetContextPages(url, body, token), path => ProfileWaiting.IsPage(path, ids), approve, cancellationToken).ConfigureAwait(true);
        return string.Equals(written.Code, ProfileWriteFlow.TimedOut, StringComparison.Ordinal)
            ? new PageSaved(PageSavedState.Failed, Message: Labels.ProfileWriteTimedOut)
            : new PageSaved(State(written.State), written.Code, written.Message);
    }

    private static PageSavedState State(ProfileWriteState state) => state switch
    {
        ProfileWriteState.Approved => PageSavedState.Approved,
        ProfileWriteState.Waiting => PageSavedState.Waiting,
        ProfileWriteState.Clash => PageSavedState.Clash,
        ProfileWriteState.Refused => PageSavedState.Refused,
        ProfileWriteState.Failed => PageSavedState.Failed,
        _ => PageSavedState.Failed,
    };

    private static PageDraft Draft(PageView row) =>
        new(row.Id, row.Name, row.Repo, PageRows.OwnerOnly(row.Key) ? null : row.Key, string.Equals(row.Mark, "owner", StringComparison.Ordinal),
            [.. row.Actions.Select(a => new PageActionDraft(a.Id, a.Name, a.Key.StartsWith("action:owner/", StringComparison.Ordinal) ? null : a.Key, a.Concepts))]);

    private static PageSetBody Body(PageDraft page) =>
        new(page.Id, page.Name, page.Repo, page.Surface, [.. page.Actions.Select(a => new PageActionSetBody(a.Id, a.Name, a.Action, a.Concepts))]);
}
