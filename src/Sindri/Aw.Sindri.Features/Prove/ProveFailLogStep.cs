using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.prove.fail-log' on the block runtime (line spec §7.3): every failure path ends here. It keeps the findings the instance holds and adds one for each recorded
/// before/after result that did not fail at the base and pass at the head (PRV-BA-001) and for each hidden scenario that did not pass (PRV-HOLD-001), the latter by its
/// visible item and what the app did, never by the scenario's id or text (line spec §9.3). A finding already held for the same rule and item is not added twice; a failure
/// that recorded nothing gets one PRV-FAIL-001 so the builder is never handed an empty list.
/// </summary>
internal sealed class ProveFailLogStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.fail-log");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var findings = new JsonArray();
        foreach (var held in (StepFacts.Read(context, "fact:findings") as JsonArray ?? []).Where(static f => f is not null))
        {
            findings.Add(held!.DeepClone()); // why: Where drops the nulls
        }

        foreach (var found in BeforeAfter(StepFacts.Read(context, "fact:beforeAfter")).Concat(Holdout(StepFacts.Read(context, "fact:holdoutResult"))))
        {
            if (!findings.Any(f => (string?)f?["rule"] == (string?)found["rule"] && (string?)f?["item"] == (string?)found["item"]))
            {
                findings.Add((JsonNode)found);
            }
        }

        if (findings.Count == 0)
        {
            findings.Add((JsonNode)Finding("PRV-FAIL-001", item: null, "the proof failed before any check recorded why (a hand test, verify or evidence run did not resolve)", "open the instance's timeline for the child that did not resolve"));
        }

        return ValueTask.FromResult(StepFacts.Done("logged", ("fact:findings", findings)));
    }

    private static IEnumerable<JsonObject> BeforeAfter(JsonNode? fact)
    {
        foreach (var result in (fact?["results"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var item = (string?)result["item"];
            if ((string?)result["base"] != "fail")
            {
                yield return Finding("PRV-BA-001", item, $"{item}: its recorded check did not fail at the base ({(string?)result["base"] ?? "no result"}), so it does not show the fix", "write a check that fails at the base and passes at the head, by the same script");
            }
            else if ((string?)result["head"] != "pass")
            {
                yield return Finding("PRV-BA-001", item, $"{item}: its recorded check did not pass at the head ({(string?)result["head"] ?? "no result"})", "make the change pass the check it was meant to fix");
            }
        }
    }

    private static IEnumerable<JsonObject> Holdout(JsonNode? fact)
    {
        foreach (var result in (fact?["results"] as JsonArray ?? []).OfType<JsonObject>().Where(static r => (string?)r["verdict"] != "pass"))
        {
            yield return HoldoutFindings.Failed((string?)result["item"], (string?)result["observed"]);
        }
    }

    private static JsonObject Finding(string rule, string? item, string message, string fix) =>
        new() { ["rule"] = rule, ["item"] = item, ["message"] = message, ["fix"] = fix, ["severity"] = "fail" };
}
