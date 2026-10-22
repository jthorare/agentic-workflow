using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.prove.holdout-log' on the block runtime (line spec §7.3, §9.3): records the evaluator's verdicts against the phase's hidden set in the holdout store. The phase key is the
/// phase packet's <c language="text">id</c>, the line is the root of the instance's address and the base of the diff is the packet's <c language="text">baseSha</c>. A report whose
/// observed line shares a run of 8 words with its scenario's text, or that names a scenario the set does not hold, fails the Step (an unrouted failure parks the instance), and so
/// does a set with no canary, a store or a commit range that can't be read. Otherwise the canary's phrase is looked for in the commits from base to head and in the facts the Step reads: a hit
/// is 'leaked' with one PRV-HOLD-003 finding and nothing else written. Failed scenarios are marked spent in the store and become PRV-HOLD-001 findings in terms of their item
/// ('failed'); otherwise the verdicts are 'passed'. The recorded <c language="text">holdoutResult</c> carries <c language="text">canary: clean</c>, each hidden scenario's result flagged
/// <c language="text">spent</c> when the store holds it spent, and the canary's own result (kind canary, no item), which is not a hidden scenario.
/// </summary>
/// <param name="store">The hidden-scenario store.</param>
/// <param name="commits">The phase's commits as text.</param>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
internal sealed class ProveHoldoutLogStep(IHoldoutStore store, ICommitText commits, IBriefFiles files, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.holdout-log");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (HoldoutLogInputs.Of(context) is not { } inputs)
        {
            return Failed("needs the instance's address and repo, fact:holdoutResult, fact:built (with its head) and fact:phasePacket (with its dir)");
        }

        var checkout = await checkouts.Of(inputs.Repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return Failed($"has no checkout of {inputs.Repo.Value}: {checkout.Error.Message}");
        }

        var (phase, baseSha, packetFailure) = await Packet(PhaseDir.Of(inputs.Dir, checkout.Value), cancellationToken).ConfigureAwait(false);
        if (phase is null || baseSha is null)
        {
            return Failed(packetFailure!); // why: no phase comes with its reason
        }

        var set = await store.Set(inputs.Line, phase, cancellationToken).ConfigureAwait(false);
        if (set.IsError)
        {
            return Failed($"can't read the phase's hidden set: {set.Error.Message}");
        }

        return await Log(inputs, new Phase(inputs.Line, phase, baseSha, checkout.Value), set.Value, cancellationToken).ConfigureAwait(false);
    }

    private static BlockStepResult Failed(string reason) => BlockStepResult.Failed("step.prove.holdout-log " + reason);

    private async ValueTask<BlockStepResult> Log(HoldoutLogInputs inputs, Phase phase, HoldoutSet set, CancellationToken cancellationToken)
    {
        if (set.Scenarios.FirstOrDefault(static s => s.IsCanary) is not { Phrase: { } phrase } canary)
        {
            return Failed("can't scan for the canary: the phase's hidden set has no canary scenario");
        }

        var (results, refusal) = Reported(inputs.Report, set);
        if (refusal is not null)
        {
            return Failed(refusal);
        }

        var text = await commits.Range(phase.Checkout, phase.BaseSha, inputs.Head, cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            return Failed($"can't read the phase's commits to scan them for the canary: {text.Error.Message}");
        }

        return inputs.ReadFacts.Append(text.Value).Any(t => t.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            ? StepFacts.Done("leaked", ("fact:findings", new JsonArray(HoldoutFindings.Leaked())))
            : await Record(inputs, phase, canary, results, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Record(HoldoutLogInputs inputs, Phase phase, HoldoutScenario canary, List<JsonObject> results, CancellationToken cancellationToken)
    {
        var spent = await store.Spent(phase.Line, phase.Key, cancellationToken).ConfigureAwait(false);
        if (spent.IsError)
        {
            return Failed($"can't read which scenarios are spent: {spent.Error.Message}");
        }

        var failed = results.Where(static r => (string?)r["verdict"] != "pass").ToList();
        if (failed.Count > 0)
        {
            var marked = await store.MarkSpent(phase.Line, phase.Key, [.. failed.Select(static r => (string)r["scenario"]!)], cancellationToken).ConfigureAwait(false); // why: Reported keeps only results that name a scenario
            if (marked.IsError)
            {
                return Failed($"can't mark the failed scenarios spent: {marked.Error.Message}");
            }
        }

        foreach (var result in results.Where(r => failed.Contains(r) || spent.Value.Contains((string)r["scenario"]!))) // why: as above
        {
            result["spent"] = true;
        }

        results.Add(new JsonObject { ["scenario"] = canary.Key, ["item"] = null, ["kind"] = HoldoutScenario.CanaryKind, ["verdict"] = "pass", ["category"] = HoldoutScenario.CanaryKind, ["observed"] = string.Empty });
        var recorded = new JsonObject { ["commit"] = inputs.Report["commit"]?.DeepClone(), ["canary"] = "clean", ["results"] = new JsonArray([.. results]) };
        return failed.Count == 0
            ? StepFacts.Done("passed", ("fact:holdoutResult", recorded))
            : StepFacts.Done("failed", ("fact:holdoutResult", recorded), ("fact:findings", new JsonArray([.. failed.Select(static r => (JsonNode)HoldoutFindings.Failed((string?)r["item"], (string?)r["observed"]))])));
    }

    // The report's hidden-scenario results, each checked against the set; a result for the canary is the Step's to write, not the evaluator's.
    private static (List<JsonObject> Results, string? Refusal) Reported(JsonObject report, HoldoutSet set)
    {
        var results = new List<JsonObject>();
        foreach (var entry in (report["results"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var key = (string?)entry["scenario"];
            if (key is not null && set.Scenarios.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal)) is { IsCanary: true })
            {
                continue;
            }

            if (key is null || set.Scenarios.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal)) is not { } scenario)
            {
                return ([], $"can't record the report: it names scenario {key ?? "(none)"}, which the phase's hidden set does not hold");
            }

            if (HoldoutOverlap.Shares((string?)entry["observed"] ?? string.Empty, [.. scenario.Steps, scenario.Expected]))
            {
                return ([], $"refuses the report for {scenario.Item}: its observed line repeats a run of {HoldoutOverlap.RunWords} or more words of the scenario's text");
            }

            results.Add((JsonObject)entry.DeepClone());
        }

        return (results, null);
    }

    private async ValueTask<(string? Phase, string? BaseSha, string? Failure)> Packet(string dir, CancellationToken cancellationToken)
    {
        var read = await files.Read("packet.json", [dir], cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return (null, null, $"can't read the phase packet {dir}: {read.Error.Message}");
        }

        try
        {
            var packet = JsonNode.Parse(read.Value.Text);
            return (string?)packet?["id"] is { } id && (string?)packet["baseSha"] is { } baseSha
                ? (id, baseSha, null)
                : (null, null, $"can't use the phase packet {dir}: packet.json must name the packet's id and baseSha");
        }
        catch (JsonException e)
        {
            return (null, null, $"can't use the phase packet {dir}: packet.json is not JSON: {e.Message}");
        }
    }

    private sealed record Phase(string Line, string Key, string BaseSha, string Checkout);
}
