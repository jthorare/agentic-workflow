using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a build job (context spec §8.7; API P6): builds the draft through the engine under the starting caller, which is the same work as the
/// 'context-rebuild' service, passes the engine's step lines on as the job's progress and records 'context.map.built' (slot: when the job began).
/// A draft with nothing left to decide (the review queue's undecided count is 0) goes through the same <see cref="RefreshGate"/> as a refresh's and is held, so the
/// 'context-map-refresh' service's next publish step promotes it when the code index allows; one with decisions left waits for a person's promote, as before (issue 1041).
/// The engine runs one build at a time and takes no heavy-job slot.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="source">Opens the recorder of the map's events under the held lease.</param>
/// <param name="outlooks">The code index's side of the publish gate.</param>
/// <param name="jobs">The running job: its progress takes the engine's lines.</param>
/// <param name="clock">Dates the slot.</param>
internal sealed class BuildContextMapService(ContextScopes scopes, IContextEngine engine, MapEventSource source, IIndexOutlooks outlooks, IJobContextAccessor jobs, TimeProvider clock)
    : IService<BuildContextMapRequest, BuildContextMapResult>
{
    private const int MaxDegraded = 20;

    public async ValueTask<Result<BuildContextMapResult>> Handle(BuildContextMapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recorder = source.Open();
        if (recorder.IsError)
        {
            return recorder.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var slot = clock.GetUtcNow();
        var built = await engine.BuildMap(jobs.Current?.Progress, scope.Value, cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var report = built.Value;
        var recorded = await recorder.Value.Built(report, slot, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        var held = await HoldWhenClean(recorder.Value, scope.Value, report, cancellationToken).ConfigureAwait(false);
        return held.IsError
            ? held.Error
            : new BuildContextMapResult(recorded.Value.Repo, recorded.Value.Draft, recorded.Value.Proposed, [.. report.Degraded.Take(MaxDegraded)]);
    }

    // A clean draft is held for the service's publish step; a queue that can't be read leaves the draft for a person's promote, never failing the build that already succeeded.
    private async ValueTask<Result<Unit>> HoldWhenClean(ContextMapEvents recorder, ContextScope scope, MapBuildReport report, CancellationToken cancellationToken)
    {
        var queue = await engine.ReviewQueue(limit: null, scope, cancellationToken).ConfigureAwait(false);
        if (queue.IsError || queue.Value.Undecided != 0)
        {
            return Unit.Value;
        }

        var gate = await new RefreshGate(engine, outlooks, clock).Run(recorder, scope, report, cancellationToken).ConfigureAwait(false);
        if (gate.IsError || gate.Value.Publish is null)
        {
            return gate.IsError ? gate.Error : Unit.Value;
        }

        // The gate would publish at once: store the hold, so the service's next publish step sees the draft and promotes it.
        var stored = await engine.Hold(new MapHold(report.Draft.Number, gate.Value.Now, report.Heads), scope, cancellationToken).ConfigureAwait(false);
        return stored.IsError ? stored.Error : Unit.Value;
    }
}
