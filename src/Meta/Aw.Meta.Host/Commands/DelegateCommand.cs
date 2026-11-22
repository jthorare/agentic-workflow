using System.Globalization;
using Aw.Meta.Features.Delegate;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'delegate' command: exit 1 when no lane was given (the lanes are listed and nothing is filed) or a non-Claude lane could not be reached (the delegation is owed).</summary>
internal static class DelegateCommand
{
    internal static CommandEntry Entry { get; } = new(
        "delegate",
        new CommandSpec("delegate --lane <lane> (--title <title> --what <what the owner wants> [--label <label>]... | --issue <n>) [--json]", [], ["lane", "title", "what", "issue"], []) { Repeated = ["label"] },
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        int? issue = null;
        if (context.Line["issue"] is { } text)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return Task.FromResult(CommandOutput.Usage(context.Io, "--issue must be an issue number", Entry.Spec.Usage));
            }

            issue = number;
        }

        var request = new DelegateRequest(context.Line["lane"], context.Line["title"], context.Line["what"], issue) { Labels = context.Line.All("label"), WakeConfirm = TimeSpan.FromSeconds(WakeConfirmSeconds(context)) };
        return CommandOutput.Call<DelegateRequest, DelegateResponse>(context, request, Text, static r => r.Delegation is null || r.SendProblem is not null);
    }

    // The same knob lane-send reads.
    private static int WakeConfirmSeconds(CommandContext context) =>
        int.TryParse(context.Io.Env("AW_META_WAKE_CONFIRM_SECONDS"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? seconds : 10;

    private static string Text(DelegateResponse response)
    {
        if (response.Delegation is not { } d)
        {
            return string.Join(
                Environment.NewLine,
                [
                    "no --lane: nothing filed. Pick a lane and run aw-meta delegate --lane <lane> again. Lanes:",
                    .. response.Lanes.Select(static l => l.Hits.Count > 0 ? $"  {l.Lane}  (matches: {string.Join(", ", l.Hits)})" : "  " + l.Lane),
                ]);
        }

        var head = string.Create(CultureInfo.InvariantCulture, $"delegated #{d.Issue} to lane {d.Lane}: {d.Url}");
        return response.Outcome == "sent"
            ? head + Environment.NewLine + "sent through lane-send"
            : string.Join(
                Environment.NewLine,
                [
                    head,
                    response.SendProblem is { } problem
                        ? $"owed, the lane did not get it: {problem}. Send it again with aw-meta lane-send --lane {d.Lane} once the lane can act (stop-check lists it until then):"
                        : $"owed: send this with SendMessage to {d.Session} (stop-check lists it until a send to {d.Session} names #{d.Issue.ToString(CultureInfo.InvariantCulture)}):",
                    d.Message,
                ]);
    }
}
