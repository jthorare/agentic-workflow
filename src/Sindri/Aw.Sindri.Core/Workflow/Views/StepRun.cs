using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// One Step run of an instance, folded from its ledger events (the instance detail's 'stepRuns', in the order the runs
/// started). Nothing here is stored by the engine: <see cref="StepRuns.Fold"/> derives it, so reading the ledger again
/// gives the same runs.
/// </summary>
/// <param name="Token">The run token ('{entry}.{n}').</param>
/// <param name="Step">The Step's state id.</param>
/// <param name="Attempt">Which try of this Step it is, from 1: the nth run of the same Step in the instance.</param>
/// <param name="MaxAttempts">The loop cap of the Step's state (see <see cref="StepRunCaps"/>): the allowance now while the active state is in the loop, else the recipe's cap; null when the state is in no loop.</param>
/// <param name="State">Running, passed, failed, refused, cancelled or abandoned.</param>
/// <param name="Runtime">Who ran it, desk or agent; null when the opening event did not say.</param>
/// <param name="StartedAt">The time of the event that opened the run.</param>
/// <param name="EndedAt">The time of the event that closed it; null while running.</param>
/// <param name="DurationMs">EndedAt minus StartedAt in milliseconds; null while running.</param>
/// <param name="Outcome">The reported outcome; null while running and for a failure.</param>
/// <param name="Tokens">The tokens an agent run spent, as its report recorded; null when none was recorded.</param>
/// <param name="Actor">Who fired the closing trigger; null while running (no event names who opened the run).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StepRun(
    string Token,
    string Step,
    int Attempt,
    int? MaxAttempts,
    StepRunState State,
    StepRuntime? Runtime,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long? DurationMs,
    string? Outcome,
    int? Tokens,
    string? Actor);
