using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.BoardLine;

/// <summary>
/// What a lane's prompt hook prints about the team board: one line naming the latest merge and the lane map version when either changed since
/// that lane last saw them, otherwise nothing. The lane's last-seen mark is the only new state.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="gates">The merges.</param>
/// <param name="lanes">The lane map.</param>
/// <param name="values">Holds each lane's last-seen mark.</param>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Red stub: nothing registers it until the service is implemented; remove with the registration.")]
internal sealed class BoardLineService(IMetaConfig config, IGateStore gates, ILaneStore lanes, IKeyValueStore values) : IService<BoardLineRequest, BoardLineResponse>
{
    public ValueTask<Result<BoardLineResponse>> Handle(BoardLineRequest request, CancellationToken cancellationToken)
    {
        _ = (config, gates, lanes, values);
        throw new NotSupportedException();
    }
}
