using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// 'step.build.checks' on the block runtime (line spec §7.2): the rules that read the diff of the worktree's head against the phase packet's base (BLD-SCOPE-001, BLD-SECRET-001, BLD-TEST-001,
/// BLD-COMMIT-001), then every one of the profile's <c language="text">workflow.build.commands</c> on the head (decision A: all run, all must pass), under one heavy slot. A secret, a changed test or a bad commit is 'failed'; a
/// path outside the allowed files alone is 'outOfScope' (the scope desk); a command that fails or runs out of time is 'failed'. 'pass' writes the measured head as 'fact:built' and
/// 'fact:change'. The head is read from the worktree, never from the builder's report. BLD-GUARD-001 (the diff guardrails) is not checked yet.
/// </summary>
/// <param name="diff">Reads the worktree's head.</param>
/// <param name="changes">Reads what the branch changed.</param>
/// <param name="files">Reads the packet in force.</param>
/// <param name="commands">Reads the profile's commands and runs them under a heavy slot.</param>
/// <param name="scrubber">Recognises secrets on added lines.</param>
internal sealed class BuildChecksStep(IRunDiff diff, IBranchChanges changes, IPacketDirectory files, RepoCommandRunner commands, ITextScrubber scrubber) : IBlockStep, IBlockStepId
{
    /// <summary>The rule id of a repo command that failed.</summary>
    internal const string CommandRule = "BLD-COMMAND-001";

    /// <summary>The rule id of a worktree with uncommitted or untracked files.</summary>
    internal const string TreeRule = "BLD-TREE-001";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.build.checks");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Text(context, "fact:workspace") is not { } worktree || ActivePacket.From(StepFacts.Read(context, "fact:activePacket")) is not { } active)
        {
            return BlockStepResult.Failed("step.build.checks needs fact:workspace (a path) and fact:activePacket ({ hash, dir })");
        }

        var head = await diff.Head(worktree, cancellationToken).ConfigureAwait(false);
        var scope = await ReadScope(active.Dir, cancellationToken).ConfigureAwait(false);
        if (head.IsError || scope.IsError)
        {
            return BlockStepResult.Failed($"step.build.checks: {(head.IsError ? head.Error : scope.Error).Message}");
        }

        var clean = await diff.IsEmpty(worktree, head.Value, cancellationToken).ConfigureAwait(false);
        if (clean.IsError || !clean.Value)
        {
            return clean.IsError
                ? BlockStepResult.Failed($"step.build.checks can't read {worktree}: {clean.Error.Message}")
                : StepFacts.Done("failed", ("fact:findings", RuleFinding.ToFact([new RuleFinding(TreeRule, $"the worktree {worktree} has uncommitted or untracked files; the checks must run on the measured head (fix: commit or discard them)")])));
        }

        var seen = await changes.Between(worktree, scope.Value.BaseSha, head.Value, cancellationToken).ConfigureAwait(false);
        return seen.IsError
            ? BlockStepResult.Failed($"step.build.checks can't read the changes of {worktree}: {seen.Error.Message}")
            : await Judge(new Branch(worktree, scope.Value, head.Value, seen.Value), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<PhaseScope>> ReadScope(string directory, CancellationToken cancellationToken)
    {
        var read = await files.Read(directory, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : PhaseScope.From(read.Value);
    }

    private async ValueTask<BlockStepResult> Judge(Branch branch, CancellationToken cancellationToken)
    {
        var scoped = BranchRules.OutOfScope(branch.Scope, branch.Changes);
        var failures = new List<RuleFinding>();
        failures.AddRange(SecretScan.Findings(BranchRules.Secret, branch.Changes.AddedLines, scrubber));
        failures.AddRange(BranchRules.ChangedTests(branch.Scope, branch.Changes));
        failures.AddRange(BranchRules.Commits(branch.Changes));
        if (failures.Count > 0)
        {
            return StepFacts.Done("failed", ("fact:findings", RuleFinding.ToFact(scoped.Concat(failures).Select(f => f.Scrubbed(scrubber)))));
        }

        if (scoped.Count > 0)
        {
            return StepFacts.Done("outOfScope", ("fact:findings", RuleFinding.ToFact(scoped.Select(f => f.Scrubbed(scrubber)))));
        }

        var ran = await RunCommands(branch.Worktree, cancellationToken).ConfigureAwait(false);
        return ran.IsError
            ? BlockStepResult.Failed($"step.build.checks: {ran.Error.Message}")
            : ran.Value.Count > 0 ? StepFacts.Done("failed", ("fact:findings", RuleFinding.ToFact(ran.Value))) : Pass(branch, scrubber);
    }

    // The profile's commands, all of them; the findings are the ones that did not pass.
    private async ValueTask<Result<IReadOnlyList<RuleFinding>>> RunCommands(string worktree, CancellationToken cancellationToken)
    {
        var listed = await commands.For(cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var ran = await commands.Run("sindri step.build.checks", listed.Value, worktree, CommandStepLimits.BuildBudget, cancellationToken).ConfigureAwait(false);
        return ran.IsError
            ? ran.Error
            : Results.Success<IReadOnlyList<RuleFinding>>([.. ran.Value.Where(static r => !r.Run.Passed).Select(static r => new RuleFinding(CommandRule, r.Summary, Detail: r.Run.Tail))]);
    }

    private static BlockStepResult Pass(Branch branch, ITextScrubber scrubber)
    {
        var commits = new JsonArray([.. branch.Changes.Commits.Select(c => (JsonNode?)new JsonObject { ["sha"] = c.Sha, ["type"] = BranchRules.CommitType(c.Subject), ["subject"] = scrubber.Scrub(c.Subject) })]);
        return StepFacts.Done(
            "pass",
            ("fact:built", new JsonObject { ["head"] = branch.Head, ["base"] = branch.Scope.BaseSha, ["commits"] = commits }),
            ("fact:change", new JsonObject { ["repo"] = branch.Worktree, ["base"] = branch.Scope.BaseSha, ["head"] = branch.Head }));
    }

    private sealed record Branch(string Worktree, PhaseScope Scope, string Head, BranchChanges Changes);
}
