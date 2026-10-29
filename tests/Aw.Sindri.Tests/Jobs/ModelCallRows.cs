using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>Builders for 'context.model.called' payloads (context model routing spec §7.1).</summary>
internal static class ModelCallRows
{
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 14, 2, 11, TimeSpan.Zero);

    /// <summary>A call that one harness and model answered.</summary>
    public static ContextModelCalled Answered(JobId? job, string callId, string harness, string model, int input, int output) =>
        new(ModelCallId.From(callId), job, "labeling", "labeling", [new ModelCallTry(harness, model, ModelCallOutcome.Answered, Reason: null, Ms: 1200, "builtin")],
            new ModelCallAnswerer(harness, model), input, output, ModelTokenBasis.Reported, Items: null, How: null, _at, Ms: 1200);

    /// <summary>A call where the first harness hit its limit and the second answered.</summary>
    public static ContextModelCalled FellBack(JobId? job, string callId, string limited, string answered, string model, int input, int output) =>
        new(ModelCallId.From(callId), job, "labeling", "labeling",
            [new ModelCallTry(limited, "haiku", ModelCallOutcome.Limit, "limit", Ms: 300, "builtin"), new ModelCallTry(answered, model, ModelCallOutcome.Answered, Reason: null, Ms: 900, "builtin")],
            new ModelCallAnswerer(answered, model), input, output, ModelTokenBasis.Estimated, Items: null, How: null, _at, Ms: 1200);

    /// <summary>A call no harness answered.</summary>
    public static ContextModelCalled Failed(JobId? job, string callId) =>
        new(ModelCallId.From(callId), job, "labeling", "labeling", [new ModelCallTry("claude-code", "haiku", ModelCallOutcome.Failed, "exit-1", Ms: 400, "builtin")],
            AnsweredBy: null, InputTokens: 0, OutputTokens: 0, ModelTokenBasis.Estimated, Items: null, How: null, _at, Ms: 400);
}
