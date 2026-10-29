namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>One model call of a job (the served 'context.model.called' payload): codes, names and numbers, never prompt or answer text.</summary>
/// <param name="CallId">The call.</param>
/// <param name="Purpose">The purpose as written in the profile docs ('labeling', 'product-surfaces', ...).</param>
/// <param name="Kind">The profile entry used ('labeling', 'conceptNaming', ...).</param>
/// <param name="Tries">Every harness tried, in order.</param>
/// <param name="AnsweredBy">The harness and model that answered, or null when none did.</param>
/// <param name="InputTokens">Input tokens.</param>
/// <param name="OutputTokens">Output tokens.</param>
/// <param name="Items">For a Jev pick, the questions in the pack (candidate groups for product surfaces); else null.</param>
/// <param name="How">For a pick, how the answers were taken; else null.</param>
/// <param name="StartedAt">When the call started.</param>
/// <param name="Ms">How long the whole call took.</param>
internal sealed record JobModelCallView(
    string CallId,
    string Purpose,
    string Kind,
    IReadOnlyList<JobModelTryView> Tries,
    JobModelAnswererView? AnsweredBy,
    int InputTokens,
    int OutputTokens,
    int? Items,
    JobModelHowView? How,
    DateTimeOffset StartedAt,
    int Ms);
