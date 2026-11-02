using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Transcripts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.LabelOutcomes;

// Labels the 'ask-check' items that have no outcome label yet from the next real prompt in their session's transcript.
// Free: no provider call. A missing transcript, a decision without a session or a missing next prompt is "no signal"
// (the item stays unlabeled and is tried again next time); only a store failure is an error.
internal sealed class LabelOutcomesService(IEvalStore items, ITranscriptReader transcripts, TimeProvider clock) : IService<LabelOutcomesRequest, LabelOutcomesResponse>
{
    public async ValueTask<Result<LabelOutcomesResponse>> Handle(LabelOutcomesRequest request, CancellationToken cancellationToken)
    {
        var candidates = await items.OutcomeCandidates(cancellationToken).ConfigureAwait(false);
        if (candidates.IsError)
        {
            return candidates.Error;
        }

        var prompts = new Dictionary<string, IReadOnlyList<TranscriptPrompt>?>(StringComparer.Ordinal);
        var (labeled, noSignal) = (0, 0);
        foreach (var candidate in candidates.Value)
        {
            var session = candidate.Session;
            var label = session is null
                ? null
                : TurnOutcomes.AskCheckLabel(candidate.Decision, candidate.DecidedAt, await PromptsOf(session.Value, prompts, cancellationToken).ConfigureAwait(false) ?? []);
            if (label is null)
            {
                noSignal++;
                continue;
            }

            var recorded = await items.RecordLabel(candidate.Item, LabelSource.Outcome, label, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            if (recorded.IsError)
            {
                return recorded.Error;
            }

            labeled++;
        }

        return new LabelOutcomesResponse(labeled, noSignal);
    }

    // One read per session; a transcript that can't be read is remembered as none.
    private async ValueTask<IReadOnlyList<TranscriptPrompt>?> PromptsOf(SessionId session, Dictionary<string, IReadOnlyList<TranscriptPrompt>?> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(session.Value, out var known))
        {
            return known;
        }

        var found = await transcripts.FindSession(session, cancellationToken).ConfigureAwait(false);
        var prompts = found.IsSuccess ? found.Value.Prompts : null;
        cache[session.Value] = prompts;
        return prompts;
    }
}
