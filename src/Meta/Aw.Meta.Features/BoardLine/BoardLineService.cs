using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.BoardLine;

/// <summary>
/// What a lane's prompt hook prints about the team board: one line naming the latest merge and the lane map version when either changed since
/// that lane last saw them, otherwise nothing. It then marks the board seen for that lane.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="board">Main's latest move and each lane's last-seen mark.</param>
/// <param name="lanes">The lane map.</param>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Red stub: nothing registers it until the service is implemented; remove with the registration.")]
internal sealed class BoardLineService(IMetaConfig config, IBoardStore board, ILaneStore lanes) : IService<BoardLineRequest, BoardLineResponse>
{
    public ValueTask<Result<BoardLineResponse>> Handle(BoardLineRequest request, CancellationToken cancellationToken)
    {
        _ = (config, board, lanes);
        throw new NotSupportedException();
    }
}
