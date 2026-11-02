using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PickReads;

/// <summary>The model.picked, model.picking and session.usage records of the pick tests, every figure named.</summary>
internal static class PickRecords
{
    public static ModelPicked Picked(
        LaunchId launch, InstanceAddress instance, string model = "claude-haiku-4-5", string by = "jev", string? reason = null, string state = "questions",
        string? from = null, string? kind = null, bool unusual = false, ItemId? item = null, int jevCalls = 1, int jevMs = 120) =>
        new(
            Launch: launch, Item: item ?? A.FirstItemId, Instance: instance, State: state, Attempt: 1, Target: AgentId.From("claude-code"), Model: model, By: by, Reason: reason,
            Decision: null, Candidates: ["m1", "m2"], BriefHash: "hash", Files: [], Omitted: [], JevMs: jevMs, JevCalls: jevCalls, ContentSent: true, Unusual: unusual,
            EscalatedFrom: from, EscalatedFromKind: kind);

    public static ModelPicking Picking(LaunchId launch, InstanceAddress instance) =>
        new(Launch: launch, Item: A.FirstItemId, Instance: instance, State: "questions", Attempt: 1, StartedAt: KnownInstants.Epoch);

    public static SessionUsage Usage(LaunchId launch, int sample, long input, long output, string? model = "claude-haiku-4-5") =>
        new(Launch: launch, Sample: sample, InputTokens: input, OutputTokens: output, Target: AgentId.From("claude-code"), Model: model);
}
