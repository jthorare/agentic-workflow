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
    public async Task<IReadOnlyList<PageDraft>?> Suggested(string? concept, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (apiUrl() is not { } url)
        {
            return null;
        }

        var wanted = new HashSet<string>(keys, StringComparer.Ordinal);
        var found = new Dictionary<string, PageDraft>(StringComparer.Ordinal);
        for (var read = 0; read < MaxReads && found.Count < wanted.Count; read++)
        {
            var answer = await api.ContextPages(url, new PagesQuery(Concept: concept, Limit: PageSize, Offset: read * PageSize, Marks: "suggested"), cancellationToken).ConfigureAwait(true);
            if (!answer.IsSuccess)
            {
                return null;
            }

            foreach (var row in answer.Value.Pages.Where(p => wanted.Contains(p.Key)))
            {
                _ = found.TryAdd(row.Key, Draft(row));
            }

            if (!answer.Value.Truncated)
            {
                break;
            }
        }

        return [.. keys.Distinct(StringComparer.Ordinal).Where(found.ContainsKey).Select(k => found[k])];
    }

    /// <inheritdoc />
    public async Task<int?> OwnerCount(CancellationToken cancellationToken)
    {
        if (apiUrl() is not { } url)
        {
            return null;
        }

        var answer = await api.ContextPages(url, new PagesQuery(Limit: 1, Marks: "owner"), cancellationToken).ConfigureAwait(true);
        return answer.IsSuccess ? answer.Value.Total : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PageMatch>?> Search(string? repo, string query, int limit, CancellationToken cancellationToken)
    {
        if (apiUrl() is not { } url)
        {
            return null;
        }

        var answer = await api.ContextPages(url, new PagesQuery(Repo: repo, Limit: limit, Marks: "all") { Q = query }, cancellationToken).ConfigureAwait(true);
        return answer.IsSuccess ? [.. answer.Value.Pages.Where(static p => !PageRows.OwnerOnly(p.Key)).Select(static p => new PageMatch(p.Key, p.Name))] : null;
    }

    /// <inheritdoc />
    public async Task<PageSaved> Save(PageSave save, string? ifHash, bool approve, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(save);
        var body = new PagesSetBody([.. save.Set.Select(Body)], [.. save.Remove], IfHash: ifHash);
        var written = await flow.Run((url, token) => api.SetContextPages(url, body, token), Mine(save), approve, cancellationToken).ConfigureAwait(true);
        return string.Equals(written.Code, ProfileWriteFlow.TimedOut, StringComparison.Ordinal)
            ? new PageSaved(PageSavedState.Failed, Message: Labels.ProfileWriteTimedOut)
            : new PageSaved(State(written.State), written.Code, written.Message);
    }

    /// <inheritdoc />
    public async Task<PagesPreviewRead> Preview(string text, string? repo, CancellationToken cancellationToken)
    {
        if (apiUrl() is not { } url)
        {
            return new PagesPreviewRead(Preview: null, "SND-API-001");
        }

        var answer = await api.ContextPagesPreview(url, text, repo, cancellationToken).ConfigureAwait(true);
        return answer.IsSuccess ? new PagesPreviewRead(answer.Value) : new PagesPreviewRead(Preview: null, answer.Error.Code, answer.Error.Message);
    }

    /// <summary>Gets which waiting key paths belong to a write: the ids it sets or removes, and, for a page with no id yet, any '.pages.' path that was not already waiting when the sheet read the profile.</summary>
    /// <param name="save">The write.</param>
    /// <returns>The test for one key path.</returns>
    internal static Func<string, bool> Mine(PageSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        List<string> ids = [.. save.Set.Select(static p => p.Id).OfType<string>(), .. save.Remove];
        var adds = save.Set.Any(static p => p.Id is null);
        return path => ProfileWaiting.IsPage(path, ids) || (adds && path.Contains(".pages.", StringComparison.Ordinal) && !save.Before.Contains(path, StringComparer.Ordinal));
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
