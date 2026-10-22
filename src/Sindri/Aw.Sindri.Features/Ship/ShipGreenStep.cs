using System.Text.Json.Nodes;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Ship;

/// <summary>
/// 'step.ship.green' on the block runtime (line spec §7.5, DTC5): the local GREEN gate on the worktree's head. First the secret scan over the lines added since the phase packet's base
/// (SHIP-SECRET-001: a hit is 'red', names the file and line, and runs nothing); then every one of the profile's <c language="text">workflow.build.commands</c> under one heavy slot (decision A: all run, all must pass). A command that fails or runs out of time
/// is 'red'. 'green' writes <c language="text">{ head, base, commands: [{ name, exitCode, seconds }] }</c> as 'fact:green'; a profile with no green command still greens, and the empty
/// list in the fact says nothing ran.
/// </summary>
/// <param name="diff">Reads the worktree's head.</param>
/// <param name="changes">Reads the lines the branch added.</param>
/// <param name="files">Reads the phase packet in force.</param>
/// <param name="commands">Reads the profile's commands and runs them under a heavy slot.</param>
/// <param name="scrubber">Recognises secrets on added lines.</param>
internal sealed class ShipGreenStep(IRunDiff diff, IBranchChanges changes, IPacketDirectory files, RepoCommandRunner commands, ITextScrubber scrubber) : IBlockStep, IBlockStepId
{
    /// <summary>The rule id of the secret scan before a push.</summary>
    internal const string SecretRule = "SHIP-SECRET-001";

    /// <summary>The rule id of a worktree with uncommitted or untracked files.</summary>
    internal const string TreeRule = "SHIP-TREE-001";

    /// <summary>The rule id of a GREEN command that failed.</summary>
    internal const string CommandRule = "SHIP-GREEN-001";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.green");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Text(context, "fact:workspace") is not { } worktree || ActivePacketOf(context) is not { } packet)
        {
            return BlockStepResult.Failed("step.ship.green needs fact:workspace (a path) and fact:phasePacket ({ hash, dir })");
        }

        var head = await diff.Head(worktree, cancellationToken).ConfigureAwait(false);
        var read = await files.Read(packet, cancellationToken).ConfigureAwait(false);
        var scope = read.IsError ? read.Error : PhaseScope.From(read.Value);
        if (head.IsError || scope.IsError)
        {
            return BlockStepResult.Failed($"step.ship.green: {(head.IsError ? head.Error : scope.Error).Message}");
        }

        if (await Dirty(worktree, head.Value, cancellationToken).ConfigureAwait(false) is { } dirty)
        {
            return dirty;
        }

        var seen = await changes.Between(worktree, scope.Value.BaseSha, head.Value, cancellationToken).ConfigureAwait(false);
        if (seen.IsError)
        {
            return BlockStepResult.Failed($"step.ship.green can't read the changes of {worktree}: {seen.Error.Message}");
        }

        var secrets = SecretScan.Findings(SecretRule, seen.Value.AddedLines, scrubber);
        return secrets.Count > 0
            ? Red(secrets)
            : await Gate(worktree, scope.Value.BaseSha, head.Value, cancellationToken).ConfigureAwait(false);
    }

    // GREEN is the gate on the pushed head: uncommitted or untracked files would make it a gate on something else.
    private async ValueTask<BlockStepResult?> Dirty(string worktree, string head, CancellationToken cancellationToken)
    {
        var clean = await diff.IsEmpty(worktree, head, cancellationToken).ConfigureAwait(false);
        return clean.IsError
            ? BlockStepResult.Failed($"step.ship.green can't read {worktree}: {clean.Error.Message}")
            : clean.Value ? null : Red([new RuleFinding(TreeRule, $"the worktree {worktree} has uncommitted or untracked files; GREEN must run on the pushed head (fix: commit or discard them)")]);
    }

    private static string? ActivePacketOf(StepContext context) => StepFacts.Field(StepFacts.Read(context, "fact:phasePacket"), "dir");

    private static BlockStepResult Red(IReadOnlyList<RuleFinding> findings) => StepFacts.Done("red", ("fact:findings", RuleFinding.ToFact(findings)));

    private async ValueTask<BlockStepResult> Gate(string worktree, string baseSha, string head, CancellationToken cancellationToken)
    {
        var listed = await commands.For(cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return BlockStepResult.Failed($"step.ship.green: {listed.Error.Message}");
        }

        var ran = await commands.Run("sindri step.ship.green", listed.Value, worktree, CommandStepLimits.GreenBudget, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return BlockStepResult.Failed($"step.ship.green: {ran.Error.Message}");
        }

        var failed = ran.Value.Where(static r => !r.Run.Passed).Select(static r => new RuleFinding(CommandRule, r.Summary, Detail: r.Run.Tail)).ToList();
        return failed.Count > 0
            ? Red(failed)
            : StepFacts.Done("green", ("fact:green", Green(baseSha, head, ran.Value)));
    }

    private static JsonObject Green(string baseSha, string head, IReadOnlyList<CheckResult> ran) => new()
    {
        ["head"] = head,
        ["base"] = baseSha,
        ["commands"] = new JsonArray([.. ran.Select(static r => (JsonNode?)new JsonObject { ["name"] = r.Name, ["exitCode"] = r.Run.ExitCode, ["seconds"] = (int)r.Run.Duration.TotalSeconds })]),
    };
}
