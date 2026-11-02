using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>
/// One model call a job's row keeps (<see cref="JobRow.ModelCalls"/>): the 'context.model.called' event without its job. The jobs projection maps the event to it, because Jobs cannot depend on Events.
/// Codes, names and numbers only.
/// </summary>
/// <param name="CallId">The call.</param>
/// <param name="Purpose">The model purpose as written in the profile docs.</param>
/// <param name="Kind">The profile entry used.</param>
/// <param name="Tries">Every harness tried, in order.</param>
/// <param name="AnsweredBy">The harness and model that answered, or null.</param>
/// <param name="InputTokens">Input tokens, as <paramref name="TokenBasis"/> says.</param>
/// <param name="OutputTokens">Output tokens.</param>
/// <param name="TokenBasis">'reported' or 'estimated'.</param>
/// <param name="Items">For a Jev pick, the questions in the pack; for a generate call, the clusters; else null.</param>
/// <param name="How">For a pick, how the answers were taken; else null.</param>
/// <param name="StartedAt">When the call started.</param>
/// <param name="Ms">How long the whole call took.</param>
public sealed record JobModelCall(
    ModelCallId CallId,
    string Purpose,
    string Kind,
    IReadOnlyList<JobModelTry> Tries,
    JobModelAnswerer? AnsweredBy,
    int InputTokens,
    int OutputTokens,
    string TokenBasis,
    int? Items,
    JobModelHow? How,
    DateTimeOffset StartedAt,
    int Ms);
