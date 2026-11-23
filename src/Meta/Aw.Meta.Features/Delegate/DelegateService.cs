using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Delegations;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Delegate;

/// <summary>
/// Turns an owner request into work a lane owns: files a GitHub issue in the team repo (or takes an existing one), records the
/// delegation and logs it. A non-Claude lane gets the ACTION message through lane-send at once; for a Claude lane the meta sends it
/// with SendMessage, and stop-check keeps the delegation owed until that send is recorded.
/// </summary>
/// <param name="config">The team config: the lanes, the repo directory and the lane keywords.</param>
/// <param name="repo">Runs gh in the team repo.</param>
/// <param name="files">The lane records: which lanes run a non-Claude agent.</param>
/// <param name="ledger">Keeps the delegations.</param>
/// <param name="log">The running log.</param>
/// <param name="delivery">lane-send's delivery, for a non-Claude lane.</param>
/// <param name="clock">Dates the delegation.</param>
internal sealed class DelegateService(IMetaConfig config, RepoCommands repo, ILaneFiles files, DelegationLedger ledger, IRunningLog log, LaneDelivery delivery, TimeProvider clock)
    : IService<DelegateRequest, DelegateResponse>
{
    public async ValueTask<Result<DelegateResponse>> Handle(DelegateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await config.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var team = loaded.Value;
        if (request.Lane is null)
        {
            return new DelegateResponse("no-lane", Delegation: null, Hints(team, request));
        }

        var lane = Lane(team, request.Lane);
        if (lane.IsError)
        {
            return lane.Error;
        }

        var record = await files.Record(lane.Value, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        var issue = await Issue(team, request, lane.Value).ConfigureAwait(false);
        if (issue.IsError)
        {
            return issue.Error;
        }

        var target = record.Value is { IsNonClaude: true, Agent: { } agent } found ? new LaneTarget(team.Team, lane.Value, found, agent) : null;
        var delegation = new Delegation(issue.Value.Number, issue.Value.Url, issue.Value.Title, lane.Value, team.Team + "-" + lane.Value, clock.GetUtcNow()) { Claude = target is null };
        return await Deliver(delegation, target, request.WakeConfirm, cancellationToken).ConfigureAwait(false);
    }

    // A non-Claude lane (target set) gets the message through lane-send's delivery now; a Claude lane's send stays owed.
    private async ValueTask<Result<DelegateResponse>> Deliver(Delegation delegation, LaneTarget? target, TimeSpan confirm, CancellationToken cancellationToken)
    {
        var before = await ledger.All(cancellationToken).ConfigureAwait(false);
        var previous = before.IsSuccess ? before.Value.LastOrDefault(d => d.Issue == delegation.Issue && !string.Equals(d.Lane, delegation.Lane, StringComparison.OrdinalIgnoreCase)) : null;
        string? problem = null;
        if (target is not null)
        {
            var sent = await delivery.Deliver(target, delegation.Message, confirm, cancellationToken).ConfigureAwait(false);
            (delegation, problem) = Outcome(delegation, sent);
        }

        var added = await ledger.Add(delegation, cancellationToken).ConfigureAwait(false);
        if (added.IsError)
        {
            return Retry(added.Error, delegation);
        }

        var state = delegation.Delivered ? DelegationState.Sent : DelegationState.Owed;
        var line = LogLine.Event(LogKind.Note, delegation.At, $"delegated {delegation.Marker} to lane {delegation.Lane} ({DelegationView.Name(state)}): {delegation.Title} {delegation.Url}");
        var appended = await log.Append(line, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return Retry(appended.Error, delegation);
        }

        var moved = previous is null ? default : await Moved(previous, delegation, confirm, cancellationToken).ConfigureAwait(false);
        return new DelegateResponse(DelegationView.Name(state), DelegationView.Of(delegation, state), []) { SendProblem = problem, LogLine = line, MovedFrom = moved.From, MovedNotice = moved.Notice, MovedProblem = moved.Problem };
    }

    // Best effort: the lane the issue moved off is told once. A non-Claude lane gets the line through lane-send's delivery; a Claude lane
    // (also one whose record now names Claude) gets it back for the meta to send with SendMessage. SendMessage can't reach a non-Claude
    // lane, so when its delivery fails or the lane can't act on it, only the problem comes back. A failed notice never fails the delegate.
    private async ValueTask<(string? From, string? Notice, string? Problem)> Moved(Delegation previous, Delegation now, TimeSpan confirm, CancellationToken cancellationToken)
    {
        var text = $"FYI: {now.Marker} moved to lane {now.Lane}; it is no longer yours.";
        if (previous.Claude)
        {
            return (previous.Session, text, null);
        }

        var record = await files.Record(previous.Lane, cancellationToken).ConfigureAwait(false);
        if (record.IsError || record.Value is null)
        {
            return (previous.Session, null, record.IsError ? $"its lane record could not be read ({record.Error.Code}: {record.Error.Message})" : $"lane {previous.Lane} has no lane record now");
        }

        if (record.Value is not { IsNonClaude: true, Agent: { } agent } found)
        {
            return (previous.Session, text, null);
        }

        var team = previous.Session[..^(previous.Lane.Length + 1)];
        var told = await delivery.Deliver(new LaneTarget(team, previous.Lane, found, agent), text, confirm, cancellationToken).ConfigureAwait(false);
        return told.IsError ? (previous.Session, null, $"{told.Error.Code}: {told.Error.Message}")
            : told.Value.Attention is { } attention ? (previous.Session, null, $"the line waits in its inbox, but lane {previous.Lane} is {attention}")
            : (previous.Session, null, null);
    }

    // Delivered only when the lane can act on it. A message stored for a lane whose hooks can't pick it up (launch-only, or its hooks
    // aren't running) stays owed, as lane-send's exit 1 says; that stored send is the attempt, which does not settle the delegation.
    private static (Delegation Delegation, string? Problem) Outcome(Delegation delegation, Result<LaneDelivered> sent) =>
        sent.IsError ? (delegation, $"{sent.Error.Code}: {sent.Error.Message}")
        : sent.Value.Attention is { } attention ? (delegation with { Attempt = sent.Value.Id }, $"lane {delegation.Lane} is {attention}")
        : (delegation with { Delivered = true }, null);

    // The issue is filed by now (and a non-Claude lane may hold the message): a plain rerun would file a second one.
    private static Error Retry(Error error, Delegation delegation) =>
        error with { Message = $"{error.Message}; issue {delegation.Marker} is filed already: rerun with aw-meta delegate --lane {delegation.Lane} --issue {delegation.Issue.ToString(CultureInfo.InvariantCulture)}" };

    // The lane as the config names it. The meta lane routes work; it never takes it.
    private static Result<string> Lane(TeamConfig team, string asked)
    {
        var work = WorkLanes(team);
        if (string.Equals(asked, team.MetaLane, StringComparison.OrdinalIgnoreCase))
        {
            return MetaErrors.UnknownLane.With($"'{asked}' is the meta lane, which routes work and never takes it; delegate to a work lane ({string.Join(", ", work)})");
        }

        return work.FirstOrDefault(l => string.Equals(l, asked, StringComparison.OrdinalIgnoreCase)) is { } name
            ? name
            : MetaErrors.UnknownLane.With($"'{asked}' is not a lane of team {team.Team} (lanes: {string.Join(", ", work)})");
    }

    private static IReadOnlyList<string> WorkLanes(TeamConfig team) =>
        [.. team.Lanes.Where(l => !string.Equals(l, team.MetaLane, StringComparison.OrdinalIgnoreCase))];

    // Every work lane, with the keywords of delegation.lanes that occur as whole words in the title or the text.
    private static IReadOnlyList<LaneHint> Hints(TeamConfig team, DelegateRequest request)
    {
        var text = (request.Title ?? string.Empty) + "\n" + (request.What ?? string.Empty);
        return [.. WorkLanes(team).Select(lane => new LaneHint(lane, [.. Keywords(team, lane).Where(k => DelegationText.HasWord(text, k))]))];
    }

    private static IReadOnlyList<string> Keywords(TeamConfig team, string lane) =>
        team.Delegation.Lanes.FirstOrDefault(p => string.Equals(p.Key, lane, StringComparison.OrdinalIgnoreCase)).Value ?? [];

    // Only an open issue is delegated: a closed one is done, and gh issue view also shows a pull request by its number.
    private static string? Refusal(IssueRef issue) =>
        issue.Url.Contains("/pull/", StringComparison.Ordinal) ? string.Create(CultureInfo.InvariantCulture, $"#{issue.Number} is a pull request: delegate an issue")
        : !issue.IsOpen ? string.Create(CultureInfo.InvariantCulture, $"#{issue.Number} is closed: reopen it or delegate an open issue")
        : null;

    private async ValueTask<Result<IssueRef>> Issue(TeamConfig team, DelegateRequest request, string lane)
    {
        if (request.Issue is { } number)
        {
            var viewed = await repo.Gh(team.Repo, "issue", "view", number.ToString(CultureInfo.InvariantCulture), "--json", "number,title,state,url").ConfigureAwait(false);
            return viewed.IsError ? viewed.Error
                : GhIssues.View(viewed.Value) is not { } found ? MetaErrors.CommandFailed.With("gh issue view printed something unexpected")
                : Refusal(found) is { } why ? MetaErrors.Invalid.With(why)
                : found;
        }

        // why: the validator refuses a request with no issue that lacks a title or a text.
        var (title, what) = (request.Title!.Trim(), request.What!.Trim());
        string[] labels = [.. request.Labels.SelectMany(static l => new[] { "--label", l.Trim() })];
        var created = await repo.Gh(team.Repo, ["issue", "create", "--title", title, "--body", what + "\n\n" + DelegationText.Footer(lane), .. labels]).ConfigureAwait(false);
        if (created.IsError)
        {
            return created.Error;
        }

        var url = created.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return url is not null && int.TryParse(url[(url.LastIndexOf('/') + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var filed)
            ? new IssueRef(filed, title, IsOpen: true, ClosedAt: null, url)
            : MetaErrors.CommandFailed.With("gh issue create printed something unexpected");
    }
}
