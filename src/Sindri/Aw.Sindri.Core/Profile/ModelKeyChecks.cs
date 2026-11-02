using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Profile;

/// <summary>The model keys' checks against this box (model-pick spec §8): the launcher catalog and the recipes' states, read when a profile is validated or approved.</summary>
/// <param name="catalog">The agent entries on this box.</param>
/// <param name="states">The states of the embedded and approved recipes; null skips the state-id check of a pin.</param>
public sealed class ModelKeyChecks(IAgentCatalog catalog, IRecipeStateCatalog? states = null)
{
    /// <summary>Agent ids against the catalog, pin states against the recipes, 'default' in 'list'.</summary>
    /// <param name="profile">The live profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>SND-MODEL-001 with the nearest id, or null.</returns>
    public async ValueTask<Error?> Models(SindriProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Workflow?.Agent is not { } agent)
        {
            return null;
        }

        var known = states is null ? null : await states.All(cancellationToken).ConfigureAwait(false);
        return known is { IsError: true }
            ? known.Error
            : ModelKeyRules.Check(agent, [.. catalog.All.Select(static e => e.Id)], known?.Value);
    }

    /// <summary>What each pin and cap matches, one line each.</summary>
    /// <param name="profile">The live profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lines; empty when the profile has no pin or cap.</returns>
    public async ValueTask<IReadOnlyList<string>> PinMatches(SindriProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Workflow?.Agent is not { } agent || (agent.Pin?.Count ?? 0) + (agent.Cap?.Count ?? 0) == 0)
        {
            return [];
        }

        var known = states is null ? null : await states.All(cancellationToken).ConfigureAwait(false);
        return ModelKeyRules.Matches(agent, known is { IsSuccess: true } ? known.Value : []);   // a failed recipe read shows no state matches; Models reports the error
    }
}
