using System.Text.Json.Nodes;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.recheck.plan' on the block runtime (PB spec §8): reads the hand-off's proof pack and lists the evidence scripts recorded with its passing results (before-after.json's head scripts and
/// proof.json's artifacts that are not superseded), each once. 'planned' carries <c language="text">recheckPlan = {headSha, workspace, scripts[]}</c>; 'nothing' is a hand-off that names no
/// proof pack. The Step fails when the pack's files cannot be read or the workspace or head is missing. The plan is also kept in the run area so the log Step, which reads no facts, can record it.
/// </summary>
/// <param name="files">Reads the proof pack's files.</param>
/// <param name="area">The instance's run area, where the plan is kept for the log.</param>
internal sealed class RecheckPlanStep(IBriefFiles files, IRunArea area) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.recheck.plan");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var head = RecheckFacts.HeadSha(RecheckFacts.Argument(context, "at"));
        if (context.Address is not { } address || head is null)
        {
            return BlockStepResult.Failed("step.recheck.plan needs the instance's address and 'at' (the PR snapshot with its head)");
        }

        var handoff = RecheckFacts.Argument(context, "handoff");
        var dir = RecheckFacts.Text(handoff?["proofPack"]?["dir"]) ?? RecheckFacts.Text(handoff?["dir"]);
        if (dir is null)
        {
            return await Done(address, "nothing", new JsonObject { ["planned"] = false, ["headSha"] = head }, null, cancellationToken).ConfigureAwait(false);
        }

        var workspace = RecheckFacts.Text(RecheckFacts.Argument(context, "workspace"));
        if (workspace is null)
        {
            return BlockStepResult.Failed("step.recheck.plan needs 'workspace' (the phase worktree's path)");
        }

        var scripts = await Scripts(dir, cancellationToken).ConfigureAwait(false);
        if (scripts is null)
        {
            return BlockStepResult.Failed($"step.recheck.plan can't read the proof pack in {dir}");
        }

        var plan = new JsonObject { ["headSha"] = head, ["workspace"] = workspace, ["scripts"] = new JsonArray([.. scripts.Select(static s => (JsonNode)JsonValue.Create(s))]) };
        var kept = (JsonObject)plan.DeepClone();
        kept["planned"] = true;
        return await Done(address, "planned", kept, plan, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Done(string address, string outcome, JsonObject kept, JsonObject? plan, CancellationToken cancellationToken)
    {
        foreach (var (name, text) in new[] { (RecheckFacts.PlanFile, kept.ToJsonString()), (RecheckFacts.ScriptsFile, new JsonObject { ["ran"] = false }.ToJsonString()) })
        {
            var written = await area.Write(address, name, text, append: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return BlockStepResult.Failed($"step.recheck.plan can't write its run area: {written.Error.Message}");
            }
        }

        return plan is null ? StepFacts.Done(outcome) : StepFacts.Done(outcome, ("fact:recheckPlan", plan));
    }

    // Null when neither file of the pack can be read.
    private async ValueTask<IReadOnlyList<string>?> Scripts(string dir, CancellationToken cancellationToken)
    {
        var found = new List<string>();
        var anyRead = false;
        foreach (var name in new[] { "before-after.json", "proof.json" })
        {
            var read = await files.Read(name, [dir], cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                continue;
            }

            anyRead = true;
            try
            {
                found.AddRange(name == "proof.json" ? ProofScripts(JsonNode.Parse(read.Value.Text)) : BeforeAfterScripts(JsonNode.Parse(read.Value.Text)));
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        return anyRead ? [.. found.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] : null;
    }

    private static IEnumerable<string> BeforeAfterScripts(JsonNode? file) =>
        (file?["results"] as JsonArray ?? []).OfType<JsonObject>().Select(static r => RecheckFacts.Text(r["headScript"])).OfType<string>();

    private static IEnumerable<string> ProofScripts(JsonNode? file) =>
        (file?["items"] as JsonArray ?? []).OfType<JsonObject>().SelectMany(static i => i["artifacts"] as JsonArray ?? [])
            .OfType<JsonObject>().Where(static a => (bool?)a["superseded"] != true).Select(static a => RecheckFacts.Text(a["script"])).OfType<string>();
}
