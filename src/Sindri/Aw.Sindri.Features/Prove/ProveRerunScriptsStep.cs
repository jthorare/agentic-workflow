using System.Text.Json.Nodes;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.prove.rerun-scripts' on the block runtime (PB spec §8): runs each script of <c language="text">fact:recheckPlan</c> in the plan's workspace. 'pass' when every one exits 0, otherwise 'fail';
/// both carry <c language="text">fact:findings</c> with one RCK-SCRIPT-001 finding per script that did not pass (a path outside the workspace is refused without running). The per-script results are
/// kept in the run area for the log Step. The Step fails when the plan or the address is missing.
/// </summary>
/// <param name="scripts">Runs one script in a worktree.</param>
/// <param name="area">The instance's run area, where the results are kept for the log.</param>
internal sealed class ProveRerunScriptsStep(IScriptRuns scripts, IRunArea area) : IBlockStep, IBlockStepId
{
    private static readonly TimeSpan _budget = TimeSpan.FromMinutes(30);

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.rerun-scripts");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = StepFacts.Read(context, "fact:recheckPlan");
        var workspace = RecheckFacts.Text(plan?["workspace"]);
        if (context.Address is not { } address || workspace is null || plan?["scripts"] is not JsonArray listed)
        {
            return BlockStepResult.Failed("step.prove.rerun-scripts needs the instance's address and fact:recheckPlan (with its workspace and scripts)");
        }

        var results = new JsonArray();
        var findings = new JsonArray();
        foreach (var script in listed.Select(RecheckFacts.Text).OfType<string>())
        {
            var (passed, detail) = await RunOne(workspace, script, cancellationToken).ConfigureAwait(false);
            results.Add((JsonNode)new JsonObject { ["script"] = script, ["passed"] = passed, ["detail"] = detail });
            if (!passed)
            {
                findings.Add((JsonNode)new JsonObject { ["rule"] = "RCK-SCRIPT-001", ["item"] = script, ["message"] = $"{script}: {detail}", ["fix"] = "make the recorded script pass on the PR's head again", ["severity"] = "fail" });
            }
        }

        var written = await area.Write(address, RecheckFacts.ScriptsFile, new JsonObject { ["ran"] = true, ["results"] = results }.ToJsonString(), append: false, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? BlockStepResult.Failed($"step.prove.rerun-scripts can't write its run area: {written.Error.Message}")
            : StepFacts.Done(findings.Count == 0 ? "pass" : "fail", ("fact:findings", findings));
    }

    private async ValueTask<(bool Passed, string Detail)> RunOne(string workspace, string script, CancellationToken cancellationToken)
    {
        if (!RecheckFacts.Inside(script))
        {
            return (false, "the recorded script path is outside the workspace, so it was not run");
        }

        return await scripts.Run(workspace, script, _budget, cancellationToken).ConfigureAwait(false) switch
        {
            0 => (true, "exit 0"),
            { } code => (false, $"it exited {code}"),
            null => (false, "it could not be run"),
        };
    }
}
