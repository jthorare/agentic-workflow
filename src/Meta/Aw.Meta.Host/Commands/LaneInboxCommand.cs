using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.LaneInbox;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'lane-inbox' command, a lane hook's call: prints the meta's messages the lane hasn't seen (they become the hook's context) and records the heartbeat. Silent when there is nothing, or no team.</summary>
internal static class LaneInboxCommand
{
    internal static CommandEntry Entry { get; } = new(
        "lane-inbox",
        CommandSpec.None("lane-inbox [--json]   (the lane comes from AW_META_TEAM and AW_META_LANE)"),
        NeedsConfig: false,   // a lane hook must pass when no team is set up
        Run);

    private static Task<int> Run(CommandContext context)
    {
        var identity = LaneIdentity.FromEnvironment(context.Io.Env);
        return identity.IsError
            ? Task.FromResult(CommandOutput.Fail(context, identity.Error))
            : CommandOutput.Call<LaneInboxRequest, LaneInboxResponse>(context, new LaneInboxRequest(identity.Value) { HookEvent = LaneInput.HookEvent(context) }, response => Text(identity.Value, response));
    }

    private static string Text(LaneIdentity lane, LaneInboxResponse response) =>
        response.Messages.Count == 0
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                [
                    $"MESSAGE FROM THE META to lane {lane.Lane}:",
                    .. response.Messages.Select(static m => "- " + m.Text.ReplaceLineEndings(" ")),
                    LaneText.HowToReply,
                ]);
}
