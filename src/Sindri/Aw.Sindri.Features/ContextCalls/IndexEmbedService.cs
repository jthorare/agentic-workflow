using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Vectors;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'index-embed' background service (owned-index spec §5.6): every minute while serve runs it asks Context for one embed run, through the facade (<see cref="IContextEngine"/>) only,
/// so every chunk that has no vector for the pinned model gets one, 4,096 chunks at a time, newest-updated record first. A run with nothing to embed is quiet. A model that is down is the
/// "meaning search is off" state, not a failed run (the next tick tries again; what was stored stays); so is a missing or unapproved profile. 'sindri index embed pause|resume' is
/// 'services pause|resume index-embed', the scheduler's own pause.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="embed">Context's facade: its embed verb.</param>
internal sealed class IndexEmbedService(ContextScopes scopes, IContextEngine embed) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("index-embed");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Gives the indexed records their meaning vectors.";

    /// <summary>Runs without a profile entry, every minute: a tick with nothing to embed is one read of the store.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            // no approved profile, or no context section, is nothing to embed, not a failed run (a failed run would land in needs-you every tick)
            return scope.IsError && scope.Error.Code != SindriErrors.ProfileNotApproved.Code ? scope.Error : ScheduledResult.Quiet;
        }

        var report = await embed.Embed(new EmbedRecordsRequest(), scope.Value, cancellationToken).ConfigureAwait(false);
        if (report.IsError)
        {
            return report.Error.Code == ContextErrors.EmbedderUnavailable.Code
                ? new ScheduledResult(Idle: true, "meaning search is off: the embedding model is not available; the next tick tries again")
                : report.Error;
        }

        return Summarize(report.Value);
    }

    /// <summary>The run's outcome: quiet when nothing was embedded, else how many chunks and whether any are left.</summary>
    internal static ScheduledResult Summarize(EmbedRecordsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Embedded == 0)
        {
            return ScheduledResult.Quiet;
        }

        var left = report.Done ? "none left" : "more to embed";
        return new ScheduledResult(Idle: false, string.Create(CultureInfo.InvariantCulture, $"ok: embedded {report.Embedded} chunks ({report.Stored} stored), {left}"));
    }
}
