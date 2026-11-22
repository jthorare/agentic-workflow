using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.HomeSession;

namespace Aw.Sindri.Features.GetHomeRow;

/// <summary>One Home row by id, in any state (home spec §8): a read of the Home view; SND-HOME-002 when it holds none. A session sees no body of a row with no repo, and another repo's row is not found for it (<see cref="HomeSessionCut"/>).</summary>
/// <param name="view">The Home view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetHomeRowService(IHomeViewReader view, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetHomeRowRequest, GetHomeRowResponse>
{
    public async ValueTask<Result<GetHomeRowResponse>> Handle(GetHomeRowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("a Home row needs a caller");
        }

        var row = await view.Get(request.Id, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is { } found && HomeSessionCut.Sees(caller, found)
            ? new GetHomeRowResponse(status.Current.AsOf, HomeSessionCut.For(caller, found))
            : SindriErrors.HomeNotFound.With($"No Home row {request.Id.Value}.");
    }
}
