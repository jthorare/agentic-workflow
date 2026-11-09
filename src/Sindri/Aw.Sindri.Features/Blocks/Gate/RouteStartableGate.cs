using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.route.startable' (spec §12, engine P7 plan D7): a stub over the profile's 'workflow.route.startable'.
/// 'all' (the default, also when no profile yet) passes; 'none' fails with the reason a human reads.
/// </summary>
/// <param name="profile">The profile.</param>
internal sealed class RouteStartableGate(IApprovedProfile profile) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("gate.route.startable"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        return EvaluateAsync(cancellationToken);
    }

    private async ValueTask<GateResult> EvaluateAsync(CancellationToken cancellationToken)
    {
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return new GateResult(GateVerdict.Fail, Score: null, [$"the profile couldn't be read: {approved.Error.Message}"]);
        }

        return approved.Value is not { } loaded || RouteSettings.StartableOf(loaded.Profile) ? Pass : Stopped;
    }

    private static GateResult Pass { get; } = new(GateVerdict.Pass, Score: null, Reasons: []);

    private static GateResult Stopped { get; } = new(GateVerdict.Fail, Score: null,
        Reasons: ["workflow.route.startable is 'none' in the profile, so the router stops this item (fix: set it to 'all', then resume)"]);
}
