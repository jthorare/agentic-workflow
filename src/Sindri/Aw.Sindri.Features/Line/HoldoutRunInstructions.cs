using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>What 'step.prove.holdout' is told (line spec §9.2, §9.3): run the phase's hidden scenarios from the read-only set and report fact:holdoutResult in the shape 'step.prove.holdout-log' reads (schemas/line/holdout-result.json).</summary>
internal sealed class HoldoutRunInstructions : IAgentStepInstructions
{
    private static readonly string[] _lines =
    [
        "Goal: run every hidden scenario of this phase against the built head in the running app, and report what you observed for each.",
        "The scenarios are in set.json in the read-only holdout directory your launch mounts: { \"scenarios\": [ { \"id\", \"item\", \"kind\", \"steps\", \"expected\" } ] }. Run them from there; never open them for edit, copy them anywhere, or write their steps or expected text into a file, a commit or a comment. Run the app from your own checkout at the head, never from the phase worktree.",
        "Write fact:holdoutResult as { \"head\": \"<the built head sha>\", \"scenarios\": [ { \"id\": \"<the scenario's id>\", \"item\": \"<its visible item id>\", \"verdict\": \"pass|fail\", \"category\": \"<one short word, e.g. functional, error, edge>\", \"observed\": \"<what the app did, at most 200 characters>\" } ] } and nothing else; no other fields.",
        "Report one entry for every scenario in set.json, canary included: it ran if it has an entry. Use verdict pass only when the app did what the scenario expected, otherwise fail; a scenario you could not run is a fail whose observed line says why.",
        "The observed line says what the app did and never repeats the scenario's steps or expected text: a run of 8 or more words shared with them parks the Step. An id not in set.json parks it too.",
        "Report ran with fact:holdoutResult, also when scenarios failed: Sindri records the verdicts.",
    ];

    private static readonly BlockId _step = BlockId.From("step.prove.holdout");

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;
}
