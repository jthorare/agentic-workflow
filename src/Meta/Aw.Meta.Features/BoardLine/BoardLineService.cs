using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.BoardLine;

/// <summary>
/// What a lane's prompt hook prints about the team board: one line naming main's latest move (short sha and the pull requests that merged
/// with it) and the lane map version when either changed since that lane last saw them, otherwise nothing. It then marks the board seen
/// for that lane. With no team set up, or nothing on the board yet, it answers null and records nothing.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="board">Main's latest move and each lane's last-seen mark.</param>
/// <param name="lanes">The lane map.</param>
internal sealed class BoardLineService(IMetaConfig config, IBoardStore board, ILaneStore lanes) : IService<BoardLineRequest, BoardLineResponse>
{
    private const int MaxLength = 200;
    private const int MaxPrs = 5;
    private const int ShortSha = 7;

    public async ValueTask<Result<BoardLineResponse>> Handle(BoardLineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error.Code == MetaErrors.NoTeamConfig.Code ? new BoardLineResponse(Line: null) : team.Error;
        }

        var lane = request.Identity.Lane;
        var loaded = await Load(lane, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var (main, laneMapVersion, seen) = loaded.Value;
        var current = new BoardSeen(main?.Version ?? 0, laneMapVersion);
        if (!Changed(current, seen))
        {
            return new BoardLineResponse(Line: null);
        }

        var marked = await board.MarkSeen(lane, current, cancellationToken).ConfigureAwait(false);
        return marked.IsError ? marked.Error : new BoardLineResponse(Line(main, laneMapVersion));
    }

    // Nothing to show when the board is still empty, or when the lane already saw exactly this.
    private static bool Changed(BoardSeen current, BoardSeen? seen) =>
        current != new BoardSeen(BoardVersion: 0, LaneMapVersion: 0) && current != seen;

    private async ValueTask<Result<(BoardFacts? Main, int LaneMapVersion, BoardSeen? Seen)>> Load(string lane, CancellationToken cancellationToken)
    {
        var main = await board.Read(cancellationToken).ConfigureAwait(false);
        var map = await lanes.Current(cancellationToken).ConfigureAwait(false);
        var seen = await board.SeenVersion(lane, cancellationToken).ConfigureAwait(false);
        if (main.IsError || map.IsError || seen.IsError)
        {
            return main.IsError ? main.Error : map.IsError ? map.Error : seen.Error;
        }

        return (main.Value, map.Value.Version, seen.Value);
    }

    private static string Line(BoardFacts? main, int laneMapVersion)
    {
        var parts = new List<string>();
        if (main is not null)
        {
            var sha = main.MainSha.Length > ShortSha ? main.MainSha[..ShortSha] : main.MainSha;
            var prs = main.MergedPrs.Take(MaxPrs).Select(pr => "#" + pr.ToString(CultureInfo.InvariantCulture)).ToList();
            if (main.MergedPrs.Count > MaxPrs)
            {
                prs.Add(string.Create(CultureInfo.InvariantCulture, $"+{main.MergedPrs.Count - MaxPrs} more"));
            }

            parts.Add(prs.Count == 0 ? $"main moved to {sha}" : $"main moved to {sha} (merged {string.Join(", ", prs)})");
        }

        if (laneMapVersion > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"lane map v{laneMapVersion}"));
        }

        var line = ("Board: " + string.Join("; ", parts)).ReplaceLineEndings(" ");
        return line.Length <= MaxLength ? line : line[..MaxLength];
    }
}
