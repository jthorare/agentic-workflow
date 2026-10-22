using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// 'step.build.red-check' on the block runtime (line spec §7.2, BLD-RED-001): the red tests must FAIL on the red commit, before any code. The profile's
/// <c language="text">workflow.build.commands</c> all run once in the phase worktree on that commit (decision A); at least one must fail ('red'). When every command passes the tests can't fail: 'vacuous' (a finding names the
/// items and their tests) and the build goes back to 'red'. A command that runs out of time, a worktree off the red commit or with uncommitted files, no red commit in the facts, no command in the profile or no test to run
/// fails the Step instead, so the instance parks. The commands run under one heavy slot.
/// </summary>
/// <param name="diff">Reads the worktree's head.</param>
/// <param name="commands">Reads the profile's commands and runs them under a heavy slot.</param>
internal sealed class BuildRedCheckStep(IRunDiff diff, RepoCommandRunner commands) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.build.red-check");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (ActivePacket.From(StepFacts.Read(context, "fact:activePacket")) is not { Repo: { } worktree })
        {
            return BlockStepResult.Failed("step.build.red-check needs fact:activePacket with the worktree as its repo (step.build.prepare sets it)");
        }

        var red = RedTests.From(StepFacts.Read(context, "fact:redTests"));
        if (red.Items.Count == 0)
        {
            return BlockStepResult.Failed("step.build.red-check found no test to run in fact:redTests ({ items: { <item>: { tests: [ids] } } })");
        }

        var listed = await commands.For(cancellationToken).ConfigureAwait(false);
        if (listed.IsError || listed.Value.Commands.Count == 0)
        {
            return BlockStepResult.Failed(listed.IsError ? listed.Error.Message : "step.build.red-check needs the profile's workflow.build.commands (fix: add the repo's build and test commands; at least one must fail on the red commit)");
        }

        return await CheckHead(red, worktree, cancellationToken).ConfigureAwait(false) is { } off
            ? off
            : await Check(red, listed.Value, worktree, cancellationToken).ConfigureAwait(false);
    }

    // The tests must run on the red commit, in a clean tree: a worktree that already moved on, or carries uncommitted files, proves nothing.
    private async ValueTask<BlockStepResult?> CheckHead(RedTests red, string worktree, CancellationToken cancellationToken)
    {
        if (red.Commit is null)
        {
            return BlockStepResult.Failed("step.build.red-check needs fact:redTests.commit, the red commit the tests are run on (step.build.red sets it)");
        }

        var head = await diff.Head(worktree, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return BlockStepResult.Failed($"step.build.red-check can't read {worktree}: {head.Error.Message}");
        }

        if (!string.Equals(head.Value, red.Commit, StringComparison.Ordinal))
        {
            return BlockStepResult.Failed($"step.build.red-check: {worktree} is at {head.Value}, not the red commit {red.Commit} (BLD-RED-001 needs the tests run before any code)");
        }

        var clean = await diff.IsEmpty(worktree, head.Value, cancellationToken).ConfigureAwait(false);
        return clean.IsError
            ? BlockStepResult.Failed($"step.build.red-check can't read {worktree}: {clean.Error.Message}")
            : clean.Value ? null : BlockStepResult.Failed($"step.build.red-check: the worktree {worktree} has uncommitted or untracked files (fix: commit or discard them, then resume)");
    }

    private async ValueTask<BlockStepResult> Check(RedTests red, CommandSet set, string worktree, CancellationToken cancellationToken)
    {
        var ran = await commands.RunUntilFailure("sindri step.build.red-check", set, worktree, CommandStepLimits.BuildBudget, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return BlockStepResult.Failed($"step.build.red-check: {ran.Error.Message}");
        }

        if (ran.Value.FirstOrDefault(static r => r.Run.TimedOut) is { } slow)
        {
            return BlockStepResult.Failed($"step.build.red-check: {slow.Summary}, so the red tests can't be judged (fix: make that command faster or split it; one command may run at most workflow.steps.longMax, and the Step at most 2 h)");
        }

        if (ran.Value.Any(static r => !r.Run.Passed))
        {
            return StepFacts.Done("red");
        }

        var tests = string.Join("; ", red.Items.Select(static item => $"item {item.Id} ({string.Join(", ", item.Tests)})"));
        var vacuous = new RuleFinding("BLD-RED-001", $"every command passed on the red commit, so the red tests of {tests} can't fail; a test that can't fail proves nothing", Detail: ran.Value[^1].Run.Tail);
        return StepFacts.Done("vacuous", ("fact:findings", RuleFinding.ToFact([vacuous])));
    }
}
