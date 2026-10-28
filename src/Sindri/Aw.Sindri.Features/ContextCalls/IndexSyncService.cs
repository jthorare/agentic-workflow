using System.Globalization;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'index-sync' background service (owned-index spec §5.5, §5.8): every 5 minutes while serve runs it asks Context to run one tick of each record feed, through
/// <see cref="IContextEngine"/> only. A source that is down is a row with its code in the run's summary, never a failed run; a missing or unapproved profile is a quiet run.
/// Where each source stands (phase, last tick, last code, the milestones usable and complete) is the facade's Progress, which the CLI and the app read.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">Context's facade: its record-feed members are Tick and Progress.</param>
internal sealed class IndexSyncService(ContextScopes scopes, IContextEngine engine) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("index-sync");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Pulls new records from each source into the index.";

    /// <summary>Runs without a profile entry, every 5 minutes.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(5)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            return scope.IsError && scope.Error.Code != SindriErrors.ProfileNotApproved.Code ? scope.Error : ScheduledResult.Quiet;
        }

        var report = await engine.Tick(scope.Value, cancellationToken).ConfigureAwait(false);
        return report.IsError ? report.Error : Summarize(report.Value.Sources);
    }

    /// <summary>The run's outcome from the feeds' rows: idle when no feed moved a record or failed, else a summary with each source's count or code.</summary>
    internal static ScheduledResult Summarize(IReadOnlyList<SourceSync> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.All(static r => r.Code is null && r.Fetched == 0 && r.Records == 0))
        {
            return ScheduledResult.Quiet;
        }

        var parts = rows.Select(static r => r.Code is null
            ? string.Create(CultureInfo.InvariantCulture, $"{r.Source.Value} {r.Fetched} fetched")
            : $"{r.Source.Value} {r.Code}");
        return new ScheduledResult(Idle: false, $"ok: {string.Join(", ", parts)}");
    }
}
