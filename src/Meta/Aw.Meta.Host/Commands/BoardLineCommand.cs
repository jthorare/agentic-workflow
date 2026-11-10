using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.BoardLine;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'board-line' command, a lane hook's call: prints one line about main and the lane map when the board changed since the lane last saw it. Silent when nothing changed, or no team.</summary>
internal static class BoardLineCommand
{
    internal static CommandEntry Entry { get; } = new(
        "board-line",
        CommandSpec.None("board-line [--json]   (the lane comes from AW_META_TEAM and AW_META_LANE)"),
        NeedsConfig: false,   // a lane hook must pass when no team is set up
        Run);

    private static Task<int> Run(CommandContext context)
    {
        var identity = LaneIdentity.FromEnvironment(context.Io.Env);
        return identity.IsError
            ? Task.FromResult(CommandOutput.Fail(context, identity.Error))
            : CommandOutput.Call<BoardLineRequest, BoardLineResponse>(context, new BoardLineRequest(identity.Value), response => response.Line ?? string.Empty);
    }
}
