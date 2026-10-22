using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// The PR babysitter's gates (PB spec §10.1): the descriptors; gate.pr.diff-scope and gate.pr.secret-scan have runtimes (PrDiffScopeGate, PrSecretScanGate), the others none until the forge port.
/// </summary>
internal static class PrGates
{
    /// <summary>'gate.revision-bound' (IDEA-009): passes only while the forge's head is still 'at', its required checks are green and nothing in mergeBlockedBy holds.</summary>
    public static BlockDescriptor RevisionBound { get; } = Of("gate.revision-bound", Text("at"), Text("pr"));

    /// <summary>'gate.pr.base-is-default' (BD4): refuses while the PR's base isn't the repo's default branch.</summary>
    public static BlockDescriptor BaseIsDefault { get; } = Of("gate.pr.base-is-default", Text("pr"));

    /// <summary>'gate.pr.autofix-cap' (IDEA-016): counts this PR's CI-cause pushes across instances and refuses at 'cap'.</summary>
    public static BlockDescriptor AutofixCap { get; } = Of("gate.pr.autofix-cap", Text("pr"), Text("cap"));

    /// <summary>'gate.pr.diff-scope' (BD2, BD5): every changed path is in scope ('phase' or 'docs'), no CI config changes and no test weakened.</summary>
    public static BlockDescriptor DiffScope { get; } = Of("gate.pr.diff-scope", Text("scope"), Text("phase"), Text("paths"));

    /// <summary>'gate.pr.secret-scan': SHIP-SECRET-001's check from 'since' to the workspace's head; a hit refuses, naming file and line only.</summary>
    public static BlockDescriptor SecretScan { get; } = Of("gate.pr.secret-scan", Text("workspace"), Text("since"));

    private static ParameterSpec Text(string name) => new(name, ParameterType.Text, Required: false, IsCapabilityField: false);

    private static BlockDescriptor Of(string id, params ParameterSpec[] parameters) => new(
        BlockId.From(id), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.None, parameters, Rung.Deterministic, DeskCapable: false);
}
