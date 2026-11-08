using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Which of a recipe's Steps run on an agent session (engine P8 D6, D7): the descriptor says <see cref="BlockRuntime.Agent"/>, the APPROVED profile
/// sets 'workflow.agent.target' and a launcher is registered. 'LoadRecipe' reads it once when an instance starts, so the answer is part of history.
/// With no target or no launcher the list is empty: a deskCapable Step runs at the desk and any other parks with SND-WF-010.
/// </summary>
/// <param name="approved">The approved profile.</param>
/// <param name="launchers">The registered launchers; none when the host has no launcher.</param>
internal sealed class AgentStepSelection(IApprovedProfile approved, IEnumerable<ILauncher> launchers)
{
    private static readonly IReadOnlyList<BlockId> _none = [];

    private static Result<IReadOnlyList<BlockId>> Wrap(IReadOnlyList<BlockId> ids) => Results.Success(ids);

    /// <summary>The agent Steps among <paramref name="used"/>, in id order.</summary>
    /// <param name="used">The descriptors of the Steps a recipe uses.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The Steps an agent runs; an error when the approvals can't be read.</returns>
    public async ValueTask<Result<IReadOnlyList<BlockId>>> Selected(IEnumerable<BlockDescriptor> used, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(used);
        if (!launchers.Any())
        {
            return Wrap(_none);
        }

        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        return latest.Value?.Profile.Workflow?.Agent?.Target is null
            ? Wrap(_none)
            : Wrap([.. used.Where(static d => d is { Kind: BlockKind.Step, Runtime: BlockRuntime.Agent }).Select(static d => d.Id).OrderBy(static id => id.Value, StringComparer.Ordinal)]);
    }
}
