using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'context.model.called' payload (context model routing spec §7.1): one model call of the context engine, written after it ends. Key 'context.model.called:&lt;callId&gt;'.
/// Plain strings and numbers only, because Context and Judge can't reference Sindri Core. It never holds prompt text, answer text, a path or CLI error text: counts, names and codes.
/// </summary>
/// <param name="CallId">A new id for the call; the idempotency key.</param>
/// <param name="Job">The job the call ran under, or null when it ran outside one (the jobs view ignores those).</param>
/// <param name="Purpose">The model purpose as written in the profile docs ('labeling', 'concept-scan', 'product-surfaces', ...).</param>
/// <param name="Kind">The profile entry used ('labeling', 'conceptNaming', ...).</param>
/// <param name="Tries">Every harness tried, in order.</param>
/// <param name="AnsweredBy">The harness and model of the try that answered, or null when none did.</param>
/// <param name="InputTokens">Input tokens, as <paramref name="TokenBasis"/> says.</param>
/// <param name="OutputTokens">Output tokens, as <paramref name="TokenBasis"/> says.</param>
/// <param name="TokenBasis">'reported' (the CLI said) or 'estimated' (characters / 4); see <see cref="ModelTokenBasis"/>.</param>
/// <param name="Items">For a Jev pick, the questions in the pack (candidate groups for 'product-surfaces'); for a generate call, the clusters; else null.</param>
/// <param name="How">For a pick, how the answers were taken; else null.</param>
/// <param name="StartedAt">When the call started.</param>
/// <param name="Ms">How long the whole call took.</param>
public sealed record ContextModelCalled(
    ModelCallId CallId,
    JobId? Job,
    string Purpose,
    string Kind,
    IReadOnlyList<ModelCallTry> Tries,
    ModelCallAnswerer? AnsweredBy,
    int InputTokens,
    int OutputTokens,
    string TokenBasis,
    int? Items,
    ModelCallHow? How,
    DateTimeOffset StartedAt,
    int Ms);
