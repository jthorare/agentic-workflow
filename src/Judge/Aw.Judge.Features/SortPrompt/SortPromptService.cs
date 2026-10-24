using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SortPrompt;

/// <summary>
/// The prompt-sort.sh hook path: skip what must not be sorted (machine text, slash commands, confirmations), ask Jev once
/// for all eleven axes (heuristics alone when Jev is excluded or has no answer), plan the scaffolds, record, and update
/// the session's file. It fails open: nothing it stores can fail the prompt.
/// </summary>
internal sealed class SortPromptService(IJudgeConfigStore configs, IBatchClassifier jev, ISortSessionStore sessions, SortRecorder recorder, TimeProvider clock)
    : IService<SortPromptRequest, SortPromptResponse>
{
    private static readonly TimeSpan _minBudget = TimeSpan.FromMilliseconds(50);

    public async ValueTask<Result<SortPromptResponse>> Handle(SortPromptRequest request, CancellationToken cancellationToken)
    {
        if (PromptTier1.SkipReason(request.Prompt) is not null)
        {
            return new SortPromptResponse();
        }

        // A reader of config.json gets the defaults when it can't be read.
        var config = ConfigReading.From(await configs.Load(cancellationToken).ConfigureAwait(false)).Match(static r => r.Config, static _ => JudgeConfig.Default);
        var settings = PromptSortSettings.From(config.PromptSort);
        if (!settings.Enabled || config.Questions.GetValueOrDefault("prompt-sort")?.Enabled == false)
        {
            return new SortPromptResponse();
        }

        var context = await SortAndRecord(request, config, settings, cancellationToken).ConfigureAwait(false);
        return new SortPromptResponse(context.Length == 0 ? null : context);
    }

    private async ValueTask<string> SortAndRecord(SortPromptRequest request, JudgeConfig config, PromptSortSettings settings, CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var sent = recorder.Redact(request.Prompt);
        var outcome = await Sort(request, config, settings, sent, cancellationToken).ConfigureAwait(false);
        var latency = clock.GetElapsedTime(started);

        var before = await Session(request.Session, cancellationToken).ConfigureAwait(false);
        var plan = ScaffoldPlanner.Plan(outcome, settings, before, request.Prompt);
        await recorder.Record(new SortRecording(request.Prompt, sent, request.Session, outcome, plan, latency), cancellationToken).ConfigureAwait(false);
        if (request.Session is { } session)
        {
            // A state file we can't write only costs the cooldown.
            _ = await sessions.Write(session, SortSessionUpdate.Next(before, outcome, plan, ScaffoldPlanner.NamesWorkflow(request.Prompt)), cancellationToken).ConfigureAwait(false);
        }

        return plan.Context;
    }

    private async ValueTask<SortSession> Session(SessionId? session, CancellationToken cancellationToken) =>
        session is { } id
            ? (await sessions.Read(id, cancellationToken).ConfigureAwait(false)).Match(static s => s, static _ => SortSession.Empty)
            : SortSession.Empty;

    // One batched request carrying the redacted, capped prompt only. Every way Jev has no answer is the heuristics alone.
    private async ValueTask<SortOutcome> Sort(SortPromptRequest request, JudgeConfig config, PromptSortSettings settings, string sent, CancellationToken cancellationToken)
    {
        if (!JevInclusion.Of(config.Providers, request.Providers))
        {
            return PromptSorter.Heuristic(request.Prompt, "no-jev");
        }

        var batch = new BatchRequest(JsonValue.Create(sent), PromptSorter.Questions, settings.Budget < _minBudget ? _minBudget : settings.Budget);
        var answered = await jev.Classify(batch, config.Providers, request.Providers, cancellationToken).ConfigureAwait(false);
        return answered switch
        {
            BatchDecided decided => PromptSorter.Blend(request.Prompt, decided.Answers),
            // A real API error or a timeout is a failure 'judge health' counts; a missing key or a dropped connection is just no judge this time.
            ProviderErrored errored => PromptSorter.Heuristic(request.Prompt, errored.ReasonCode, errored.ReasonCode),
            ProviderUnavailable { ReasonCode: ProviderUnavailable.Timeout } timeout => PromptSorter.Heuristic(request.Prompt, timeout.ReasonCode, timeout.ReasonCode),
            _ => PromptSorter.Heuristic(request.Prompt, answered.ReasonCode),
        };
    }
}
