using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// Turns the tries of a call into its 'context.model.called' event (model routing spec §7.1) and hands it to the log. A call that parks writes its tries so far before it parks, so the Jobs page shows the limit while the job waits; the
/// event after the resume holds the later tries. A call outside a job is written only when it failed or hit a limit. Never prompt text, answer text, a path or CLI error text: names, numbers and codes.
/// </summary>
/// <param name="log">Where the event goes.</param>
/// <param name="ids">Names each event.</param>
/// <param name="clock">Dates the event.</param>
internal sealed class ModelCallRecorder(IModelCallLog log, IIdGenerator ids, TimeProvider clock)
{
    /// <summary>Starts the call's first segment now.</summary>
    /// <param name="call">The call.</param>
    internal void Begin(ModelCall call)
    {
        call.SegmentAt = clock.GetUtcNow();
        call.SegmentStamp = clock.GetTimestamp();
    }

    /// <summary>Writes the tries since the last write, when they are worth an event, and starts the next segment.</summary>
    /// <param name="call">The call.</param>
    /// <param name="answer">The answer that ends the call, or null (a failure, or a park).</param>
    /// <returns>A task that completes when the event is written or has failed and been logged.</returns>
    internal async ValueTask Write(ModelCall call, ModelAnswer? answer)
    {
        var tries = call.Tries;
        var earlier = call.Written;
        var startedAt = call.SegmentAt;
        var ms = (int)Math.Min(clock.GetElapsedTime(call.SegmentStamp).TotalMilliseconds, int.MaxValue);
        call.Written += tries.Count;
        call.Tries = [];
        Begin(call);
        if (Worth(call, tries, answer, earlier))
        {
            await log.Record(Build(call, tries, answer, (startedAt, ms)), CancellationToken.None).ConfigureAwait(false);
        }
    }

    // An empty segment is worth an event only when it is the whole call (no harness installed); a call outside a job only when it failed or fell back.
    private static bool Worth(ModelCall call, List<ModelCallTry> tries, ModelAnswer? answer, int earlier) =>
        tries.Count == 0 ? earlier == 0 : call.Job is not null || answer is null || tries.Exists(static t => t.Outcome != ModelCallOutcome.Answered);

    private ContextModelCalled Build(ModelCall call, List<ModelCallTry> tries, ModelAnswer? answer, (DateTimeOffset At, int Ms) segment) => new(
        ModelCallId.From(ids.NewId().ToString("N", CultureInfo.InvariantCulture)),
        call.Job?.Job,
        PurposeName(call.Request.Purpose),
        ModelKinds.Key(call.Kind),
        tries,
        answer?.Harness is { } harness ? new ModelCallAnswerer(harness, answer.Model ?? string.Empty) : null,
        answer?.InputTokens ?? 0,
        answer?.OutputTokens ?? 0,
        answer is { TokenBasis: TokenBasis.Reported } ? ModelTokenBasis.Reported : ModelTokenBasis.Estimated,
        Items: null,
        How: null,
        segment.At,
        segment.Ms);

    private static string PurposeName(ModelPurpose purpose) => purpose switch
    {
        ModelPurpose.Labeling => "labeling",
        ModelPurpose.DecisionExtraction => "decision-extraction",
        ModelPurpose.Rerank => "rerank",
        ModelPurpose.Adjudication => "adjudication",
        ModelPurpose.Cleanup => "map-cleanup",
        ModelPurpose.Concepts => "concepts",
        ModelPurpose.ConceptSynthesis => "concept-synthesis",
        ModelPurpose.ConceptScan => "concept-scan",
        ModelPurpose.ProductSurfaces => "product-surfaces",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };
}
