using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The desk runtime's timeout (engine spec W20, §3.4, §3.8): a desk Step's wait is at least the profile's
/// 'workflow.deskTimeout' (default 2d), and its expiry always parks with 'timeout'; 'then: retry' and 'then: escalate'
/// are replaced by 'park' in desk mode. Pure, so the interpreter and its unit tests call the same code. Whether a timer
/// has expired is decided by TransitionDecision.Expired, which builds its park with <see cref="TimeoutPark"/>.
/// </summary>
public static class DeskTimeouts
{
    /// <summary>The 'workflow.deskTimeout' default (W20); the profile key (S1-D11, WorkflowTimes) overrides it.</summary>
    public static TimeSpan DefaultFloor { get; } = TimeSpan.FromDays(2);

    /// <summary>How long a desk Step waits for its report: the state's timeout, raised to <paramref name="floor"/>.</summary>
    /// <param name="timeout">The state's timeout; an active state always has one (§3.4).</param>
    /// <param name="floor">The profile's 'workflow.deskTimeout'.</param>
    /// <returns>The wait.</returns>
    public static TimeSpan Wait(TimeoutSpec? timeout, TimeSpan floor) =>
        timeout is { After: var after } && after.Span > floor ? after.Span : floor;

    /// <summary>The 'timeout' park (spec §3.8): '{state} ran past {after}'; '--resume' goes back to the frame with a fresh deadline.</summary>
    /// <param name="state">The state that ran past its timeout.</param>
    /// <param name="after">How long it had.</param>
    /// <param name="actor">The timer's actor.</param>
    /// <returns>The park.</returns>
    internal static Suspension TimeoutPark(string state, TimeSpan after, string actor) => new(
        SuspensionKind.Park, ParkReason.Timeout, ParkKind: null, Escalation: null, RanPast(state, after), actor, Mirrors: null,
        new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effects: []));

    /// <summary>What a timeout park or escalation says (spec §3.8): '{state} ran past {after}'.</summary>
    internal static string RanPast(string state, TimeSpan after) => $"{state} ran past {Text(after)}";

    /// <summary>A duration as status writes it (spec §6.4): '2d', '2d 1h', '4h 30m', or '40s' under a minute.</summary>
    private static string Text(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalSeconds}s");
        }

        var parts = new List<string>(3);
        if (span.Days > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Days}d"));
        }

        if (span.Hours > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Hours}h"));
        }

        if (span.Minutes > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}m"));
        }

        return string.Join(' ', parts);
    }
}
