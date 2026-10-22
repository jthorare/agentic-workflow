using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The refresh job's publish gate (code index spec 6.4): decides whether the draft it just settled goes ahead. A decision that doesn't depend on the clock publishes at once; a wait stores the hold, which reads
/// back the start of the wait (an earlier hold of the chain keeps it), and decides again, so a wait that already ran out publishes. The outcome is a publish, or the draft held (with 'context.map.held' appended).
/// </summary>
/// <param name="engine">The context engine: the stored publish state and the hold.</param>
/// <param name="outlooks">The code index's side of the gate.</param>
/// <param name="clock">The time.</param>
internal sealed class RefreshGate(IContextEngine engine, IIndexOutlooks outlooks, TimeProvider clock)
{
    /// <summary>What the gate decided: <paramref name="Publish"/> is null when the draft is held.</summary>
    internal readonly record struct Outcome(MapPublish? Publish, MapHoldState Hold, DateTimeOffset Now);

    internal async ValueTask<Result<Outcome>> Run(ContextMapEvents recorder, ContextScope scope, MapBuildReport report, CancellationToken cancellationToken)
    {
        var wait = MapGate.Wait(scope);
        var state = await engine.Held(scope, cancellationToken).ConfigureAwait(false);
        if (wait.IsError || state.IsError)
        {
            return wait.IsError ? wait.Error : state.Error;
        }

        var now = clock.GetUtcNow();
        var begun = new MapHold(report.Draft.Number, now, report.Heads);
        var first = await Decide(state.Value, MapGate.StateOf(begun), wait.Value, now, cancellationToken).ConfigureAwait(false);
        if (first.IsError || first.Value is not MapWait)
        {
            return first.IsError ? first.Error : Publishes(first.Value, MapGate.StateOf(begun), now);
        }

        return await Holding(recorder, (scope, state.Value, wait.Value), report, begun, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Outcome>> Holding(ContextMapEvents recorder, (ContextScope Scope, MapPublishState State, TimeSpan Wait) read, MapBuildReport report, MapHold begun, CancellationToken cancellationToken)
    {
        var stored = await engine.Hold(begun, read.Scope, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var holding = MapGate.StateOf(stored.Value);
        var second = await Decide(read.State, holding, read.Wait, begun.Since, cancellationToken).ConfigureAwait(false);
        if (second.IsError || second.Value is not MapWait waiting)
        {
            return second.IsError ? second.Error : Publishes(second.Value, holding, begun.Since);
        }

        var held = new ContextMapHeld(report.Draft.Repo.Value, holding.Version, holding.Heads.Count, waiting.WaitingOn, (int)read.Wait.TotalMinutes, Chained: holding.Since < begun.Since);
        var recorded = await recorder.HeldFor(held, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new Outcome(Publish: null, holding, begun.Since);
    }

    private async ValueTask<Result<MapPublishAction>> Decide(MapPublishState state, MapHoldState hold, TimeSpan wait, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var read = await outlooks.Read(hold, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : MapPublishDecision.Decide(MapGate.Input(state, hold, read.Value, wait), now);
    }

    // A refresh decides about the draft it just built, so a drop (the draft is not the latest, or is already current) means another writer got there: the 190 a stale promote would have answered.
    private static Result<Outcome> Publishes(MapPublishAction action, MapHoldState hold, DateTimeOffset now) =>
        action is MapPublish publish
            ? new Outcome(publish, hold, now)
            : ContextErrors.DraftChanged.With(string.Create(CultureInfo.InvariantCulture, $"draft v{hold.Version} is no longer the latest draft"));
}
