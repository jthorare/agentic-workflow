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
/// The 'index-decisions' background service (#1597): every 15 minutes while serve runs it asks Context to read the records already pulled into the index for decisions, through
/// <see cref="IContextEngine"/> only. It is apart from 'index-sync' so a slow model never holds a pull. A source with a code is a row in the run's summary, never a failed run; a missing or unapproved profile is a quiet run.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">Context's facade: its decision member is Decide.</param>
internal sealed class IndexDecisionsService(ContextScopes scopes, IContextEngine engine) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("index-decisions");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Finds decisions in the records already pulled into the index.";

    /// <summary>Runs without a profile entry, every 15 minutes.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(15)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            return scope.IsError && scope.Error.Code != SindriErrors.ProfileNotApproved.Code ? scope.Error : ScheduledResult.Quiet;
        }

        var report = await engine.Decide(scope.Value, cancellationToken).ConfigureAwait(false);
        return report.IsError ? report.Error : Summarize(report.Value.Sources);
    }

    /// <summary>The run's outcome from the sources' rows: idle when no source had a record to read or failed, else a summary with each source's count or code.</summary>
    internal static ScheduledResult Summarize(IReadOnlyList<SourceDecisions> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.All(static r => r.Code is null && r.Looked == 0))
        {
            return ScheduledResult.Quiet;
        }

        var parts = rows.Select(static r => r.Code is null
            ? string.Create(CultureInfo.InvariantCulture, $"{r.Source.Value} {r.Looked} read, {r.Decisions} decisions")
            : $"{r.Source.Value} {r.Code}");
        return new ScheduledResult(Idle: false, $"ok: {string.Join(", ", parts)}");
    }
}
