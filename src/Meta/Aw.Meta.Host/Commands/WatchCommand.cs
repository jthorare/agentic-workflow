using Aw.Meta.Core.Watch;
using Aw.Meta.Features.Watch;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'watch' command: prints each line as it happens (for Monitor) and exits 0 when it expires.</summary>
internal static class WatchCommand
{
    private static readonly TimeSpan _defaultInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan _defaultDuration = TimeSpan.FromMinutes(29);

    internal static CommandEntry Entry { get; } = new(
        "watch",
        new CommandSpec("watch [--interval 120] [--for 29m] [--until-mail] [--json]", [], ["interval", "for"], ["until-mail"]),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        if (!Duration(context.Line["interval"], _defaultInterval, out var interval) || !Duration(context.Line["for"], _defaultDuration, out var duration))
        {
            return Task.FromResult(CommandOutput.Usage(context.Io, "--interval and --for take a duration such as 120, 90s, 29m or 2h", Entry.Spec.Usage));
        }

        async ValueTask Print(string line)
        {
            await context.Io.Out.WriteLineAsync(line.AsMemory(), context.Cancellation).ConfigureAwait(false);
            await context.Io.Out.FlushAsync(context.Cancellation).ConfigureAwait(false);
        }

        return CommandOutput.Call<WatchRequest, WatchResponse>(context, new WatchRequest(interval, duration, Print) { UntilMail = context.Line.Has("until-mail") }, static _ => string.Empty);
    }

    private static bool Duration(string? text, TimeSpan fallback, out TimeSpan value)
    {
        value = fallback;
        return text is null || WatchDuration.TryParse(text, out value);
    }
}
