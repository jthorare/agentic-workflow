using System.Globalization;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.LaneStopCheck;

namespace Aw.Meta.Host.Commands;

/// <summary>
/// The 'lane-stop-check' command, a lane's stop hook: exit 1 means "block this stop" (the owed messages are printed with how to reply),
/// exit 0 lets it through (silent, or one warning line when the loop guard let a stop through), exit 2 is a tool error the hook passes.
/// </summary>
internal static class LaneStopCheckCommand
{
    internal static CommandEntry Entry { get; } = new(
        "lane-stop-check",
        new CommandSpec("lane-stop-check [--stop-hook-active] [--json]   (the lane comes from AW_META_TEAM and AW_META_LANE)", [], [], ["stop-hook-active"]),
        NeedsConfig: false,   // a lane must be able to stop when no team is set up
        Run);

    private static Task<int> Run(CommandContext context)
    {
        var identity = LaneIdentity.FromEnvironment(context.Io.Env);
        if (identity.IsError)
        {
            return Task.FromResult(CommandOutput.Fail(context, identity.Error));
        }

        var max = int.TryParse(context.Io.Env("AW_META_STOP_MAX"), NumberStyles.None, CultureInfo.InvariantCulture, out var configured) && configured > 0
            ? configured
            : StopCheckCommand.DefaultMaxBlocks;
        var request = new LaneStopCheckRequest(identity.Value, context.Line.Has("stop-hook-active"), max) { HookEvent = LaneInput.HookEvent(context) };
        return CommandOutput.Call<LaneStopCheckRequest, LaneStopCheckResponse>(context, request, response => Text(identity.Value, response), static r => r.Blocked);
    }

    private static string Text(LaneIdentity lane, LaneStopCheckResponse response)
    {
        if (response.Warning is { } warning)
        {
            return warning;
        }

        return response.Blocked
            ? string.Join(
                Environment.NewLine,
                [
                    string.Create(CultureInfo.InvariantCulture, $"aw-meta: lane {lane.Lane} owes the meta a reply ({response.Owed.Count} message(s)):"),
                    .. response.Owed.Select(static m => "- " + m.Text.ReplaceLineEndings(" ")),
                    LaneText.HowToReply,
                ])
            : string.Empty;
    }
}
