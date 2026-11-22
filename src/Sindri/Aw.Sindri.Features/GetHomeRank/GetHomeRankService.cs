using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetHomeRank;

/// <summary>
/// The explain data of one Home row (home spec §4.2, §9): the rank record as the view stores it and the row's blocks list; SND-HOME-002 when the view holds no such row,
/// or a session may not see it. It reads; the ranking itself is HomeRank's, which writes the stored record.
/// </summary>
/// <param name="view">The Home view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetHomeRankService(IHomeViewReader view, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetHomeRankRequest, GetHomeRankResponse>
{
    public async ValueTask<Result<GetHomeRankResponse>> Handle(GetHomeRankRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("a Home rank needs a caller");
        }

        var row = await view.Get(request.Id, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        if (row.Value is not { } found || !HomeSessionCut.Sees(caller, found))
        {
            return SindriErrors.HomeNotFound.With($"No Home row {request.Id.Value}.");
        }

        var shown = HomeSessionCut.For(caller, found);
        return new GetHomeRankResponse(status.Current.AsOf, shown.Id, shown.Kind, shown.Since, shown.Rank, shown.BlocksList);
    }
}
