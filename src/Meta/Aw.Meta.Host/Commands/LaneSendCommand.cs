using Aw.Meta.Features.LaneSend;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'lane-send' command: the meta's send to a non-Claude lane. Exit 1 when the message was stored but the lane can't act on it (launch-only, or its hooks aren't running).</summary>
internal static class LaneSendCommand
{
    internal static CommandEntry Entry { get; } = new(
        "lane-send",
        new CommandSpec("lane-send --lane <lane> (--text <text> | --file <path>) [--json]", ["lane"], ["text", "file"], []),
        NeedsConfig: true,
        Run);

    private static async Task<int> Run(CommandContext context)
    {
        var text = await LaneInput.Read(context, allowStdin: false).ConfigureAwait(false);
        return text.IsError
            ? CommandOutput.Fail(context, text.Error)
            : await CommandOutput.Call<LaneSendRequest, LaneSendResponse>(context, new LaneSendRequest(context.Line.Value("lane"), text.Value) { WakeConfirmSeconds = WakeConfirmSeconds(context) }, Text, static r => r.Attention is not null).ConfigureAwait(false);
    }

    private static int WakeConfirmSeconds(CommandContext context) =>
        int.TryParse(context.Io.Env("AW_META_WAKE_CONFIRM_SECONDS"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? seconds : 10;

    private static string Text(LaneSendResponse response) =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                $"sent {response.Id.Value} to lane {response.Lane}; " + (response.Woken ? "woken" + (response.ViewerAttached ? " (a viewer is attached)" : string.Empty) : "not woken: " + response.WakeSkipped),
                response.Attention is { } attention ? $"attention: lane {response.Lane} is {attention}" : null,
            }.OfType<string>());
}
