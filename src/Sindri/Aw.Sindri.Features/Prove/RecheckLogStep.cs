using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.recheck.log' on the block runtime (PB spec §8): writes <c language="text">fact:recheck = {headSha, verify, scripts[], verdict}</c> from the plan and the rerun the earlier Steps kept in the
/// run area (the descriptor reads no facts). 'held' when the plan was made, the scripts ran and every one passed; 'broken' when one failed or the scripts never ran (the verify journey did
/// not resolve); 'nothing' when there was no hand-off to re-run or no plan at all. The head is recorded even then, so the proven head moves.
/// </summary>
/// <param name="area">The instance's run area, where the plan and the rerun were kept.</param>
internal sealed class RecheckLogStep(IRunArea area) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.recheck.log");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Address is not { } address)
        {
            return BlockStepResult.Failed("step.recheck.log needs the instance's address");
        }

        var plan = await Read(address, RecheckFacts.PlanFile, cancellationToken).ConfigureAwait(false);
        var ran = await Read(address, RecheckFacts.ScriptsFile, cancellationToken).ConfigureAwait(false);
        var head = RecheckFacts.Text(plan?["headSha"]);
        if (plan is null || (bool?)plan["planned"] != true)
        {
            return Verdict("nothing", head, "skipped", []);
        }

        if ((bool?)ran?["ran"] != true)
        {
            return Verdict("broken", head, "failed", []);
        }

        var results = new JsonArray((ran["results"] as JsonArray ?? []).Where(static r => r is not null).Select(static r => r!.DeepClone()).ToArray());
        var allPassed = results.OfType<JsonObject>().All(static r => (bool?)r["passed"] == true);
        return Verdict(allPassed ? "held" : "broken", head, "resolved", results);
    }

    private async ValueTask<JsonNode?> Read(string address, string name, CancellationToken cancellationToken)
    {
        var read = await area.Read(address, name, cancellationToken).ConfigureAwait(false);
        try
        {
            return read.IsError ? null : JsonNode.Parse(read.Value);
        }
        catch (JsonException)
        {
            return null;   // a file that is not JSON is as good as none: the recheck is then 'nothing' or 'broken', never a pass
        }
    }

    private static BlockStepResult Verdict(string verdict, string? head, string verify, JsonArray scripts) =>
        StepFacts.Done(verdict, ("fact:recheck", new JsonObject { ["headSha"] = head, ["verify"] = verify, ["scripts"] = scripts, ["verdict"] = verdict }));
}
