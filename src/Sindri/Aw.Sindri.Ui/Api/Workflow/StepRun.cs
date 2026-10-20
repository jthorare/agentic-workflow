namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One Step run of an instance, as GET /workflow/instances/{id} serves it ('stepRuns').</summary>
/// <param name="Token">The run token ('{entry}.{n}').</param>
/// <param name="Step">The Step's state id.</param>
/// <param name="Attempt">Which try of this Step it is, from 1.</param>
/// <param name="MaxAttempts">The loop cap of the Step's state; null when it is in no loop.</param>
/// <param name="State">running, passed, failed, refused, cancelled or abandoned.</param>
/// <param name="Runtime">desk or agent; null when not recorded.</param>
/// <param name="StartedAt">When the run opened.</param>
/// <param name="EndedAt">When it closed; null while running.</param>
/// <param name="DurationMs">Milliseconds from start to end; null while running.</param>
/// <param name="Outcome">The reported outcome; null while running and for a failure.</param>
/// <param name="Tokens">The tokens an agent run spent, when recorded.</param>
/// <param name="Actor">Who fired the closing trigger; null while running.</param>
internal sealed record StepRun(
    string Token,
    string Step,
    int Attempt,
    int? MaxAttempts,
    string State,
    string? Runtime,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long? DurationMs,
    string? Outcome,
    int? Tokens,
    string? Actor);
