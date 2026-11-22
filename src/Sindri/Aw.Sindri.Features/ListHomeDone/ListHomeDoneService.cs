using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListHomeDone;

/// <summary>
/// The Done list (home spec §5.4): every done row of the Home view marked done in the last <see cref="KeepDays"/> days, the most recently done first, then by id.
/// A session sees only its repo's rows, and no body of a row with no repo (<see cref="HomeSessionCut"/>).
/// </summary>
/// <param name="view">The Home view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
/// <param name="clock">Now, for the keep window.</param>
internal sealed class ListHomeDoneService(IHomeViewReader view, IProjectorStatus status, ICallerContextAccessor callers, TimeProvider clock) : IService<ListHomeDoneRequest, ListHomeDoneResponse>
{
    /// <summary>The default of the profile key home.done.keepDays (home spec §5.4); the key itself lands with the profile keys.</summary>
    internal const int KeepDays = 7;

    public async ValueTask<Result<ListHomeDoneResponse>> Handle(ListHomeDoneRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("the Done list needs a caller");
        }

        var done = new List<HomeRow>();
        string? after = null;
        do
        {
            // why: the view pages by rank, not by when a row was done, so the list reads every done page before it orders them
            var page = await view.List(new HomeQuery(Group: null, [HomeState.Done], after, HomePaging.MaxLimit, HomeSessionCut.ScopeOf(caller)), cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            done.AddRange(page.Value.Rows);
            after = page.Value.Cursor;
        }
        while (after is not null);

        var since = clock.GetUtcNow().AddDays(-KeepDays);
        HomeRow[] rows =
        [
            .. done.Where(r => r.DoneAt is { } at && at >= since)
                .OrderByDescending(static r => r.DoneAt)
                .ThenBy(static r => r.Id.Value, StringComparer.Ordinal)
                .Select(r => HomeSessionCut.For(caller, r)),
        ];
        return new ListHomeDoneResponse(status.Current.AsOf, KeepDays, rows);
    }
}
