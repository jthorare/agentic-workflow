using System.Globalization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// The Agents page's reasons (Agents page spec §3.2): a pure function from what the collector read to the ordered reasons that hold now. It reads nothing and keeps nothing; each reason
/// carries the time it began, taken from its source, so the page can rank rows by when they first held a class.
/// </summary>
public static class AgentReasons
{
    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    /// <summary>The reasons that hold for one row, lowest class first; the first is the primary. Empty when none holds.</summary>
    /// <param name="inputs">What was read for the row.</param>
    /// <returns>The reasons, ordered by class and then by when they began.</returns>
    public static IReadOnlyList<AgentReason> Evaluate(AgentReasonInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        AgentReason?[] single = [Asking(inputs), UsageLimit(inputs), NoSession(inputs), RecycleMark(inputs), IdleOwed(inputs), SlotWait(inputs)];
        return [.. single.OfType<AgentReason>().Concat(PrReady(inputs)).OrderBy(static r => r.Class)];   // why: OrderBy is stable, so the PRs stay oldest first
    }

    // Class 1: the session asks (approval, input or auth).
    private static AgentReason? Asking(AgentReasonInputs i)
    {
        if (i.State != SessionState.Asking || i.Asking is not { } asking)
        {
            return null;
        }

        return asking.Reason switch
        {
            AskingReason.Approval => new AgentReason(
                1, AgentReasonKind.PermissionPrompt, "asking", asking.Tool is { Length: > 0 } tool ? $"{i.Name}: asking to run {tool}" : $"{i.Name}: asking for approval", asking.Since, i.StateSource),
            AskingReason.Input => new AgentReason(1, AgentReasonKind.OwnerQuestion, "question", $"{i.Name}: asking you a question", asking.Since, i.StateSource),
            AskingReason.Auth => new AgentReason(1, AgentReasonKind.Login, "login", $"{i.Name}: needs a login", asking.Since, i.StateSource),
            _ => null,
        };
    }

    // Class 2 (Waiting): the lane hit its usage limit.
    private static AgentReason? UsageLimit(AgentReasonInputs i)
    {
        if (!i.UsageLimited)
        {
            return null;
        }

        var resets = i.UsageResetsAt?.ToString("HH:mm", _invariant);
        return new AgentReason(
            2, AgentReasonKind.UsageLimit, resets is null ? "limited" : $"back at {resets}",
            resets is null ? $"{i.Name}: hit its usage limit, reset time unknown" : $"{i.Name}: hit its usage limit, resets {resets}", i.UsageSince ?? i.Now, AgentStateSource.AwMeta);
    }

    // Class 3: a lane in the team map with no live session, unless the owner stopped it from the page.
    private static AgentReason? NoSession(AgentReasonInputs i) =>
        i.Lane is not null && !i.HasLiveSession && !i.StoppedFromPage
            ? new AgentReason(3, AgentReasonKind.LaneNoSession, "no session", $"{i.Name}: no live session", i.StateSince, AgentStateSource.AwMeta)
            : null;

    // Class 4: the lane's context is at the recycle mark; for the meta lane, also an open RecycleSelf obligation.
    private static AgentReason? RecycleMark(AgentReasonInputs i)
    {
        if (!i.AtRecycleMark && !(i.IsMetaLane && i.RecycleSelfOpen))
        {
            return null;
        }

        var text = i.ContextTokens is { } tokens
            ? string.Create(_invariant, $"{i.Name}: at {(tokens + 500) / 1000}k, recycle from a digest")
            : $"{i.Name}: recycle from a digest";
        return new AgentReason(4, AgentReasonKind.RecycleMark, "recycle", text, i.RecycleSince ?? i.Now, AgentStateSource.AwMeta);
    }

    // Class 5: a lane session idle for the limit, with work owed to it.
    private static AgentReason? IdleOwed(AgentReasonInputs i)
    {
        if (i.Lane is null || i.State != SessionState.Idle || i.Estimated)
        {
            return null;
        }

        var idle = i.Now - i.StateSince;
        var owed = Owed(i);
        if (idle < TimeSpan.FromMinutes(i.IdleOwedMinutes) || owed.Count == 0)
        {
            return null;
        }

        return new AgentReason(
            5, AgentReasonKind.IdleOwed, "work owed", string.Create(_invariant, $"{i.Name}: idle {(int)idle.TotalMinutes} min, {string.Join(", ", owed)}"),
            i.StateSince.AddMinutes(i.IdleOwedMinutes), AgentStateSource.AwMeta);
    }

    // Spec §3.2 rules 3a to 3c: a PR that failed its gate or conflicts, the meta lane's open obligations, mail a non-Claude lane was not given.
    private static List<string> Owed(AgentReasonInputs i)
    {
        List<string> owed = [];
        foreach (var pr in i.QueuedPrs.Where(static p => p.Gate is AgentGate.Fail or AgentGate.Conflict))
        {
            owed.Add(string.Create(_invariant, $"PR #{pr.Number} {(pr.Gate == AgentGate.Fail ? "failed its gate" : "has a conflict")}"));
        }

        if (i.IsMetaLane && i.OpenObligations > 0)
        {
            owed.Add(string.Create(_invariant, $"{i.OpenObligations} open obligation{(i.OpenObligations == 1 ? string.Empty : "s")}"));
        }

        if (!i.IsClaudeLane && i.MailUndeliveredMinutes is { } mail)
        {
            owed.Add(string.Create(_invariant, $"mail not delivered for {mail} min"));
        }

        return owed;
    }

    // Class 6 (Waiting): a heavy waiter that waited past the limit.
    private static AgentReason? SlotWait(AgentReasonInputs i)
    {
        if (i.HeavyWait is not { } wait || i.Now - wait.Since < TimeSpan.FromMinutes(i.SlotWaitMinutes))
        {
            return null;
        }

        return new AgentReason(
            6, AgentReasonKind.SlotWait, "waiting for a slot",
            string.Create(_invariant, $"{i.Name}: waiting {(int)(i.Now - wait.Since).TotalMinutes} min for a heavy slot ({wait.Held} of {wait.Cap} held)"), wait.Since, AgentStateSource.AwMeta);
    }

    // Class 7: each PR the lane sent that waits for the owner's merge (gate passed or not run, and Meta has not queued it yet).
    private static IEnumerable<AgentReason> PrReady(AgentReasonInputs i) =>
        i.QueuedPrs
            .Where(static p => !p.InGithubQueue && p.Gate is AgentGate.Pass or AgentGate.None)
            .OrderBy(static p => p.ReadyAt).ThenBy(static p => p.Number)
            .Select(p => new AgentReason(7, AgentReasonKind.PrReady, "PR ready", string.Create(_invariant, $"{i.Name}: PR #{p.Number} ready in the merge queue"), p.ReadyAt, AgentStateSource.AwMeta));
}
