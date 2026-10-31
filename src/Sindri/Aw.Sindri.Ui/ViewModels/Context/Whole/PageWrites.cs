using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The app's side of <see cref="IPageWrites"/>: reads the profile and the map's pages through the API, and writes pages with the SetPages job. The write is sent once; the job is polled until it ends; the hash it wrote is approved only
/// when nothing but these pages waits (the write-then-approve rule the pins copy, concept pins spec §3.6).
/// </summary>
/// <param name="api">Sindri's API.</param>
/// <param name="apiUrl">The control plane's API address now, or null when none is known.</param>
/// <param name="clock">The clock for the job poll.</param>
internal sealed class PageWrites(ISindriApi api, Func<Uri?> apiUrl, TimeProvider clock) : IPageWrites
{
    private const string Clash = "SND-PROFILE-003";
    private const int PageSize = 200;
    private const int MaxReads = 5;
    private const int MaxPolls = 120;
    private static readonly TimeSpan _pollEvery = TimeSpan.FromMilliseconds(250);

    /// <inheritdoc />
    public async Task<ProfileWaiting?> Waiting(CancellationToken cancellationToken)
    {
        if (apiUrl() is not { } url)
        {
            return null;
        }

        var read = await api.ProfileSummary(url, cancellationToken).ConfigureAwait(true);
        return read.IsSuccess ? new ProfileWaiting(read.Value.Live, read.Value.ChangedPaths ?? []) : null;
    }

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
        if (apiUrl() is not { } url)
        {
            return new PageSaved(PageSavedState.Failed);
        }

        var body = new PagesSetBody([.. save.Set.Select(Body)], [.. save.Remove], IfHash: ifHash);
        var started = await api.SetContextPages(url, body, cancellationToken).ConfigureAwait(true);
        if (!started.IsSuccess)
        {
            return Ended(started.Error.Code, started.Error.Message);
        }

        var job = await Wait(url, started.Value.JobId, cancellationToken).ConfigureAwait(true);
        if (job.Error is { } error)
        {
            return Ended(error.Code, error.Message);
        }

        if (job.View is not { State: "succeeded" } done)
        {
            return job.View is null ? new PageSaved(PageSavedState.Failed, Message: Labels.ContextPagesSaveTimedOut) : Ended(job.View.Code, job.View.Summary);
        }

        var hash = HashOf(done.Result);
        return approve && hash is not null ? await Approve(url, hash, [.. save.Set.Select(p => p.Id), .. save.Remove], cancellationToken).ConfigureAwait(true) : new PageSaved(PageSavedState.Waiting);
    }

    // Approves the hash the job wrote only when nothing but these pages waits; otherwise it stays for Review and approve.
    private async Task<PageSaved> Approve(Uri url, string hash, IReadOnlyList<string> pageIds, CancellationToken cancellationToken)
    {
        if (await Waiting(cancellationToken).ConfigureAwait(true) is not { } waiting || !waiting.OnlyPages(pageIds))
        {
            return new PageSaved(PageSavedState.Waiting);
        }

        var approved = await api.ApproveProfile(url, new ApproveProfileRequest(hash), cancellationToken).ConfigureAwait(true);
        if (approved.IsSuccess)
        {
            return new PageSaved(PageSavedState.Approved);
        }

        return string.Equals(approved.Error.Code, Clash, StringComparison.Ordinal) ? new PageSaved(PageSavedState.Waiting) : Ended(approved.Error.Code, approved.Error.Message);
    }

    // The job's last read: its view once it is not running, or null view when it still ran after the wait; an error when the poll itself failed.
    private async Task<(Api.Jobs.JobView? View, Error? Error)> Wait(Uri url, string jobId, CancellationToken cancellationToken)
    {
        for (var poll = 0; poll < MaxPolls; poll++)
        {
            var read = await api.Job(url, jobId, cancellationToken).ConfigureAwait(true);
            if (!read.IsSuccess)
            {
                return (null, read.Error);
            }

            if (!string.Equals(read.Value.State, "running", StringComparison.Ordinal))
            {
                return (read.Value, null);
            }

            await Task.Delay(_pollEvery, clock, cancellationToken).ConfigureAwait(true);
        }

        return (null, null);
    }

    // What an error id means for the sheet: a moved profile, a refused page (the server's sentence), or anything else.
    private static PageSaved Ended(string? code, string? message)
    {
        if (string.Equals(code, Clash, StringComparison.Ordinal))
        {
            return new PageSaved(PageSavedState.Clash, code, message);
        }

        var refused = code is { Length: 14 } && code.StartsWith("SND-CONTEXT-25", StringComparison.Ordinal) && code[^1] is >= '0' and <= '6';
        return new PageSaved(refused ? PageSavedState.Refused : PageSavedState.Failed, code, message);
    }

    private static string? HashOf(JsonElement? result) =>
        result is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("hash", out var hash) && hash.ValueKind == JsonValueKind.String ? hash.GetString() : null;

    private static PageDraft Draft(PageView row) =>
        new(row.Id, row.Name, row.Repo, PageRows.OwnerOnly(row.Key) ? null : row.Key, string.Equals(row.Mark, "owner", StringComparison.Ordinal),
            [.. row.Actions.Select(a => new PageActionDraft(a.Id, a.Name, a.Key.StartsWith("action:owner/", StringComparison.Ordinal) ? null : a.Key, a.Concepts))]);

    private static PageSetBody Body(PageDraft page) =>
        new(page.Id, page.Name, page.Repo, page.Surface, [.. page.Actions.Select(a => new PageActionSetBody(a.Id, a.Name, a.Action, a.Concepts))]);
}
