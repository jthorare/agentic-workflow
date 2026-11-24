using System.Globalization;
using Aw.Meta.Features.Delegate;
using Aw.Meta.Features.DelegateDrop;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'delegate' command: exit 1 when no lane was given (the lanes are listed and nothing is filed), a non-Claude lane could not be reached (the delegation is owed), or --drop names an issue with no delegation.</summary>
internal static class DelegateCommand
{
    // The options --drop can't be given with (--label is a repeatable list, checked apart).
    private static readonly string[] _dropConflicts = ["lane", "title", "what", "issue"];

    internal static CommandEntry Entry { get; } = new(
        "delegate",
        new CommandSpec("delegate (--lane <lane> (--title <title> --what <what the owner wants> [--label <label>]... | --issue <n>) | --drop <n>) [--json]", [], ["lane", "title", "what", "issue", "drop"], []) { Repeated = ["label"] },
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        if (context.Line["drop"] is { } drop)
        {
            return Drop(context, drop);
        }

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

    // --drop <n> forgets the delegation of issue n; it takes no other option.
    private static Task<int> Drop(CommandContext context, string text)
    {
        var others = _dropConflicts.Where(o => context.Line[o] is not null).Select(static o => "--" + o).ToList();
        if (context.Line.All("label").Count > 0)
        {
            others.Add("--label");
        }

        if (others.Count > 0)
        {
            return Task.FromResult(CommandOutput.Usage(context.Io, "--drop takes no other option, got " + string.Join(", ", others), Entry.Spec.Usage));
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? CommandOutput.Call<DelegateDropRequest, DelegateDropResponse>(context, new DelegateDropRequest(number), DropText, static r => !r.Dropped)
            : Task.FromResult(CommandOutput.Usage(context.Io, "--drop must be an issue number", Entry.Spec.Usage));
    }

    private static string DropText(DelegateDropResponse response) =>
        response.Dropped
            ? string.Create(CultureInfo.InvariantCulture, $"dropped the delegation of #{response.Issue} to lane {response.Lane}")
            : string.Create(CultureInfo.InvariantCulture, $"no delegation of #{response.Issue} recorded: nothing dropped. A delegate of #{response.Issue} that is still delivering records it when it ends: run aw-meta delegate --drop {response.Issue} again after it");

    // The same knob lane-send reads.
    private static int WakeConfirmSeconds(CommandContext context) =>
        int.TryParse(context.Io.Env("AW_META_WAKE_CONFIRM_SECONDS"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? seconds : 10;

    private static string Text(DelegateResponse response) =>
        response.MovedFrom is not { } movedFrom ? Body(response)
        : response.MovedNotice is { } notice ? Body(response) + Environment.NewLine + $"moved off {movedFrom}: send it this with SendMessage: {notice}"
        : response.MovedProblem is { } problem ? Body(response) + Environment.NewLine + $"moved off {movedFrom}: the notice was not delivered ({problem}); it is not a Claude lane, so SendMessage can't reach it"
        : Body(response) + Environment.NewLine + $"moved off {movedFrom}: told it through lane-send";

    private static string Body(DelegateResponse response)
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
