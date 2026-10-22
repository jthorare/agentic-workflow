using System.Text.Json.Nodes;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.prove.before-after' on the block runtime (line spec §7.3, PRV-BA-001): for every live item's <c language="text">base+head</c> signal, its recorded script (the signal's
/// <c language="text">script</c>, a path inside the phase's workspace) runs at the phase's base, in a scratch worktree made for the run, and at the head, in the workspace. It is the
/// workspace's one file that runs in both trees, so the two runs are by the same script. The result is <c language="text">{commit, results[{item, signal, base, head, baseScript, headScript}]}</c>
/// in 'fact:beforeAfter'; 'pass' when every script failed at the base and passed at the head, otherwise 'fail' (a signal with no usable script is recorded as 'missing' with a note, nothing is run, and it does not fail the Step: the spec names no other runnable source).
/// The Step fails (and an unrouted failure parks the instance) when a script cannot run at all, when the scratch worktree cannot be made, or when the context or a phase file is missing.
/// The scratch worktree is removed afterwards (never forced).
/// </summary>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="area">The instance's run area, where the scratch worktree goes.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
/// <param name="worktrees">Makes and removes the scratch worktree.</param>
/// <param name="scripts">Runs a script in a checkout.</param>
internal sealed class ProveBeforeAfterStep(IBriefFiles files, IRunArea area, IRepoCheckouts checkouts, IWorktrees worktrees, IScriptRuns scripts) : IBlockStep, IBlockStepId, IRepoStep
{
    private static readonly TimeSpan _scriptTimeout = TimeSpan.FromMinutes(20);

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.before-after");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (EvidenceInputs.Of(context) is not { } inputs || StepFacts.Text(context, "fact:workspace") is not { Length: > 0 } workspace)
        {
            return BlockStepResult.Failed("step.prove.before-after needs the instance's address and repo, fact:phasePacket (with its dir), fact:built (with its head) and fact:workspace");
        }

        var (phase, checkout, failure) = await Load(inputs, cancellationToken).ConfigureAwait(false);
        if (phase is null || checkout is null)
        {
            return BlockStepResult.Failed(failure!);   // why: a null phase comes with its reason
        }

        var planned = Signals(phase).ToList();
        var (rows, unrunnable) = planned.Exists(static p => p.Script is not null)
            ? await RunInScratch(inputs.Address, (phase, checkout), planned, workspace, cancellationToken).ConfigureAwait(false)
            : (planned.Select(static p => (JsonNode)Row(p, "missing", "missing")).ToList(), null);
        if (unrunnable is not null)
        {
            return BlockStepResult.Failed(unrunnable);
        }

        var holds = rows.OfType<JsonObject>().All(static r => (string?)r["base"] == "missing" || ((string?)r["base"] == "fail" && (string?)r["head"] == "pass"));   // why: a signal with no runnable script is recorded, not failed (the demo can't block on a field nobody writes)
        return StepFacts.Done(holds ? "pass" : "fail", ("fact:beforeAfter", new JsonObject { ["commit"] = inputs.Head, ["results"] = new JsonArray([.. rows]) }));
    }

    private async ValueTask<(PhaseFiles? Phase, string? Checkout, string? Failure)> Load(EvidenceInputs inputs, CancellationToken cancellationToken)
    {
        var checkout = await checkouts.Of(inputs.Repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return (null, null, $"step.prove.before-after has no checkout of {inputs.Repo.Value}: {checkout.Error.Message}");
        }

        var dir = PhaseDir.Of(inputs.Dir, checkout.Value);
        var (phase, failure) = await PhaseFiles.Load(files, dir, cancellationToken).ConfigureAwait(false);
        return phase is null ? (null, null, $"step.prove.before-after can't load the phase packet {dir}: {failure}") : (phase, checkout.Value, null);
    }

    private async ValueTask<(List<JsonNode> Rows, string? Unrunnable)> RunInScratch(string address, (PhaseFiles Phase, string Checkout) at, List<Planned> planned, string workspace, CancellationToken cancellationToken)
    {
        var (phase, checkout) = at;
        var scratch = area.PathFor(address, "before-after/base");
        if (scratch.IsError)
        {
            return ([], $"step.prove.before-after can't place the scratch worktree: {scratch.Error.Message}");
        }

        var made = await worktrees.Create(new WorktreeRequest(checkout, phase.BaseSha, "sindri/before-after/" + phase.Id, scratch.Value), cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return ([], $"step.prove.before-after can't make the scratch worktree at the base {phase.BaseSha}: {made.Error.Code} {made.Error.Message}");
        }

        try
        {
            return await RunAll(planned, workspace, made.Value.Path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = await worktrees.Remove(made.Value.Path, CancellationToken.None).ConfigureAwait(false);   // why: a tree that can't be removed (SND-WT-004) is left as it is; the result stands
        }
    }

    private async ValueTask<(List<JsonNode> Rows, string? Unrunnable)> RunAll(List<Planned> planned, string workspace, string baseTree, CancellationToken cancellationToken)
    {
        var rows = new List<JsonNode>();
        foreach (var p in planned)
        {
            if (p.Script is null)
            {
                rows.Add(Row(p, "missing", "missing"));
                continue;
            }

            var script = Path.Combine(workspace, p.Script);
            var atBase = await scripts.Run(baseTree, script, _scriptTimeout, cancellationToken).ConfigureAwait(false);
            var atHead = await scripts.Run(workspace, script, _scriptTimeout, cancellationToken).ConfigureAwait(false);
            if (atBase is null || atHead is null)
            {
                return (rows, $"step.prove.before-after can't run {p.Script} for {p.Item} {p.Signal} at the {(atBase is null ? "base" : "head")}: no shell, no such file or a timeout");
            }

            rows.Add(Row(p, atBase == 0 ? "pass" : "fail", atHead == 0 ? "pass" : "fail"));
        }

        return (rows, null);
    }

    private static JsonObject Row(Planned p, string atBase, string atHead) => new()
    {
        ["item"] = p.Item,
        ["signal"] = p.Signal,
        ["base"] = atBase,
        ["head"] = atHead,
        ["baseScript"] = p.Script,
        ["headScript"] = p.Script,
        ["note"] = p.Script is null ? "the before/after proof was not runnable: the signal records no usable script" : null,
    };

    private static IEnumerable<Planned> Signals(PhaseFiles phase) =>
        ProofChecks.LiveItems(phase.Acceptance).SelectMany(item => (phase.Signals?["signals"]?[item] as JsonArray ?? []).OfType<JsonObject>()
            .Where(static s => (string?)s["runsAt"] == "base+head")
            .Select(s => new Planned(item, (string?)s["id"] ?? string.Empty, Script((string?)s["script"]))));

    // A script is a path inside the workspace: relative, no '..' and no drive.
    private static string? Script(string? path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Split('/', '\\').Contains("..", StringComparer.Ordinal) ? path : null;

    private sealed record Planned(string Item, string Signal, string? Script);
}
