using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// What the session routes and the silence sweep share (API spec §8.1, launcher L-9): the secret check, 'launch.session-reported' (the first valid id wins,
/// always before the launch's end) and 'session.ended' (with the cleanup of the launch's files). Every event carries the launch id.
/// </summary>
/// <param name="files">The launch's hash and per-launch files.</param>
/// <param name="liveness">The heartbeats.</param>
/// <param name="admission">The item of the launch and whether the entry accepts a reported id.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Appends the events.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="clock">The time.</param>
public sealed class LaunchSessionEvents(ILaunchFiles files, LaunchLiveness liveness, ReportedIdAdmission admission, HeldLease held,
    ILedgerStore ledger, EventFactory events, TimeProvider clock)
{
    /// <summary>How long the end of a session waits for the token totals before it ends without them.</summary>
    public static TimeSpan FinalTokensBudget { get; } = TimeSpan.FromSeconds(5);

    /// <summary>Checks a call's secret against the launch's stored hash.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="secret">The secret the call carried.</param>
    /// <returns>Success, or SND-AUTH-003.</returns>
    public Result<Unit> Verify(LaunchId launch, LaunchSecret secret) => files.Verify(launch, secret);

    /// <summary>Starts the silence clock again and records the session id the heartbeat carries, once.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="reported">The agent's session id, or null.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or why the ledger refused.</returns>
    public async ValueTask<Result<Unit>> Beat(LaunchId launch, SessionId? reported, CancellationToken cancellationToken)
    {
        var reports = await Report(launch, reported, cancellationToken).ConfigureAwait(false);
        if (reports.IsSuccess || IsPatternTimeout(reports.Error))
        {
            liveness.Beat(launch, clock.GetUtcNow());   // after the report: a heartbeat that comes back as an error doesn't restart the silence clock; a pattern timeout says nothing about the agent
        }

        return reports;
    }

    /// <summary>Whether <paramref name="error"/> says only that the session id pattern timed out (the launch itself is fine).</summary>
    /// <param name="error">The error from <see cref="Report"/>.</param>
    /// <returns>True for SND-LAUNCH-030.</returns>
    public static bool IsPatternTimeout(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return string.Equals(error.Code, SindriErrors.LaunchSessionIdTimedOut.Code, StringComparison.Ordinal);
    }

    /// <summary>Records the agent's session id as 'launch.session-reported', once, when the entry's pattern accepts it.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="reported">The agent's session id, or null.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, SND-LAUNCH-030 when the entry's pattern timed out on the id (nothing is recorded), or why the ledger refused.</returns>
    public async ValueTask<Result<Unit>> Report(LaunchId launch, SessionId? reported, CancellationToken cancellationToken)
    {
        if (reported is not { } id || liveness.IsReported(launch))
        {
            return Unit.Value;
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var admitted = await admission.Admit(launch, id, cancellationToken).ConfigureAwait(false);
        if (admitted.TimedOut)
        {
            return SindriErrors.LaunchSessionIdTimedOut.With("the entry's sessionIdPattern timed out on the reported session id, so no id is recorded (the heartbeat still counts)");
        }

        if (!admitted.Accepted)
        {
            return Unit.Value;   // the entry's pattern refuses it: the heartbeat still counts, the id isn't recorded
        }

        var entry = events.Entry(EventTypes.LaunchSessionReported, new LaunchSessionReported(launch, id), $"{EventTypes.LaunchSessionReported}:{launch.Value}", admitted.Item);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        liveness.MarkReported(launch);
        return Unit.Value;
    }

    /// <summary>Appends 'session.ended', then deletes the launch's files and forgets its clock.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="reason">Exit, lost or limit.</param>
    /// <param name="exitCode">The exit code for an exit.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or why the ledger refused (the files stay).</returns>
    public ValueTask<Result<Unit>> End(LaunchId launch, SessionEndReason reason, int? exitCode, CancellationToken cancellationToken) =>
        Append(new SessionEnded(launch, reason, exitCode), cancellationToken);

    /// <summary>Appends 'session.ended' for a sandbox limit kill (reason limit, with the kind, the limit and the peaks), then deletes the launch's files and forgets its clock.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="limit">Which limit, its value and the peaks.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or why the ledger refused (the files stay).</returns>
    public ValueTask<Result<Unit>> EndByLimit(LaunchId launch, LimitEnd limit, CancellationToken cancellationToken) =>
        Append(SessionEnded.Limited(launch, limit), cancellationToken);

    private async ValueTask<Result<Unit>> Append(SessionEnded ended, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var launch = ended.Launch;
        var admitted = await admission.Admit(launch, id: null, cancellationToken).ConfigureAwait(false);
        var (input, output) = await TokensWithin(launch, cancellationToken).ConfigureAwait(false);
        var entry = events.Entry(EventTypes.SessionEnded, ended with { InputTokens = input, OutputTokens = output }, $"{EventTypes.SessionEnded}:{launch.Value}", admitted.Item);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        files.Forget(launch);
        liveness.End(launch);
        return Unit.Value;
    }

    /// <summary>The final token totals, best effort: a read that takes longer than <see cref="FinalTokensBudget"/> gives none, so the end of the session never waits on a transcript.</summary>
    private async ValueTask<(long? Input, long? Output)> TokensWithin(LaunchId launch, CancellationToken cancellationToken)
    {
        using var bound = new CancellationTokenSource(FinalTokensBudget, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bound.Token);
        try
        {
            return await admission.FinalTokens(launch, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, null);
        }
    }

    /// <summary>Appends one private 'session.usage' sample.</summary>
    /// <param name="item">The launch's item.</param>
    /// <param name="usage">The launch, the sample's number and the totals so far.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or why the ledger refused.</returns>
    public async ValueTask<Result<Unit>> Usage(string item, SessionUsage usage, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{EventTypes.SessionUsage}:{usage.Launch.Value}:{usage.Sample}");
        var appended = await ledger.Append(events.Entry(EventTypes.SessionUsage, usage, key, item), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
