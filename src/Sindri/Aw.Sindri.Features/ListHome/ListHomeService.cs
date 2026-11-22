using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.HomeSession;

namespace Aw.Sindri.Features.ListHome;

/// <summary>
/// A page of Home rows (home spec §8): one read of the Home view for the open rows (or the snoozed ones), each group in ranked order. A session sees only its repo's rows, and no body of a row with no repo (<see cref="HomeSessionCut"/>); its counts cover what it sees.
/// The page says 'behind' when the views trail the ledger.
/// </summary>
/// <param name="view">The Home view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class ListHomeService(IHomeViewReader view, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListHomeRequest, ListHomeResponse>
{
    public async ValueTask<Result<ListHomeResponse>> Handle(ListHomeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("Home needs a caller");
        }

        HomeState[] states = [request.Snoozed ? HomeState.Snoozed : HomeState.Open];
        var page = await view.List(new HomeQuery(request.Split, states, request.After, request.Limit, HomeSessionCut.ScopeOf(caller)), cancellationToken).ConfigureAwait(false);
        if (page.IsError)
        {
            return page.Error;
        }

        var rows = page.Value.Rows.Select(r => HomeSessionCut.For(caller, r)).ToList();
        var asOf = status.Current.AsOf;
        var behind = asOf.LedgerHead - asOf.Cursor;
        IReadOnlyList<HomeDegraded> degraded = behind > 0
            ? [new HomeDegraded("behind", $"{behind.ToString(CultureInfo.InvariantCulture)} event{(behind == 1 ? string.Empty : "s")}")]
            : [];
        return new ListHomeResponse(asOf, page.Value.Counts, [.. rows.Where(static r => r.Group == HomeGroup.NeedsYou)], [.. rows.Where(static r => r.Group == HomeGroup.Moving)], page.Value.Cursor, degraded);
    }
}
