using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>
/// Adds how a sandbox limit ended a run to its 'step.failed' row (sandbox spec §8): the run's launch id is the engine's, so 'launch.outcome:&lt;launch&gt;' says when the session started and which item it ran for, and
/// 'session.ended:&lt;launch&gt;' says which limit ended it and when. The branch is the run branch when it is ahead of the repo's checkout (its work is kept), else null. Anything unreadable leaves the row as it was.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads the payloads.</param>
/// <param name="views">The items view, for the item's repo.</param>
/// <param name="approved">The approved profile, for the repo's checkout.</param>
/// <param name="branches">Says whether the run branch kept commits.</param>
internal sealed class LimitEndingReader(ILedgerReader ledger, EventFactory events, IViewReader views, IApprovedProfile approved, IRunBranches branches)
{
    private const string LimitReason = "limit";   // the reason a Step a sandbox limit ended fails with (Core's Unavailables.LimitReason)

    /// <summary>Adds the limit ending to <paramref name="fields"/> when they are a limit failure (reason 'limit') of an agent run.</summary>
    /// <param name="fields">The row's fields.</param>
    /// <param name="instance">The instance the row belongs to.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The fields, with the ending when it could be read.</returns>
    public async ValueTask<InstanceEventFields> Add(InstanceEventFields fields, WorkflowInstanceId instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (!string.Equals(fields.Reason, LimitReason, StringComparison.Ordinal) || fields.Run is not { } text || !RunToken.TryFrom(text, out var run))
        {
            return fields;
        }

        var launch = AgentLaunches.LaunchIdFor(instance, run);
        var ended = await Read<SessionEnded>("session.ended:" + launch.Value, cancellationToken).ConfigureAwait(false);
        var started = await Read<LaunchStarted>("launch.outcome:" + launch.Value, cancellationToken).ConfigureAwait(false);
        if (ended is not { Payload: { Reason: SessionEndReason.Limit, Kind: { } kind, Limit: { } limit } } || started is null)
        {
            return fields;
        }

        var seconds = Math.Max((long)(ended.At - started.At).TotalSeconds, 0);
        var branch = await KeptBranch(started.Payload.Item, instance, run, cancellationToken).ConfigureAwait(false);
        return fields with { Limit = new InstanceLimitEnding(kind == LimitKind.Memory ? "memory" : "wall", Written(kind, limit), seconds.ToString(CultureInfo.InvariantCulture), branch) };
    }

    private static string Written(LimitKind kind, long limit) => kind == LimitKind.Memory ? SandboxUnits.FormatSize(limit) : SandboxUnits.FormatDuration(TimeSpan.FromSeconds(limit));

    /// <summary>The run branch when the item's repo checkout can be found and the branch is ahead of it; null otherwise.</summary>
    private async ValueTask<string?> KeptBranch(ItemId item, WorkflowInstanceId instance, RunToken run, CancellationToken cancellationToken)
    {
        var row = await views.Get<ItemRow>(ViewNames.Items, item.Value, cancellationToken).ConfigureAwait(false);
        var loaded = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var path = row.IsSuccess && row.Value?.Repo is { } repo && loaded.IsSuccess
            ? loaded.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal))?.Path
            : null;
        var branch = string.Create(CultureInfo.InvariantCulture, $"sindri/{instance.Value}/{run.Number}/work");
        return path is not null && await branches.HasCommits(path, branch, cancellationToken).ConfigureAwait(false) ? branch : null;
    }

    private async ValueTask<Recorded<TPayload>?> Read<TPayload>(string key, CancellationToken cancellationToken)
    {
        var row = await ledger.ByKey(key, cancellationToken).ConfigureAwait(false);
        if (row is not { IsSuccess: true, Value: { } found })
        {
            return null;
        }

        var payload = events.Payload<TPayload>(found);
        return payload.IsSuccess ? new Recorded<TPayload>(payload.Value, found.Envelope.OccurredAt) : null;
    }

    private sealed record Recorded<TPayload>(TPayload Payload, DateTimeOffset At);
}
