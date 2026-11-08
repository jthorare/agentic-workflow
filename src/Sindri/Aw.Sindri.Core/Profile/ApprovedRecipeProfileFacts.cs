using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Profile;

/// <summary>The recipe validator's profile facts (spec §9.1, S1-D11) read from the approved profile: its effect allowlist and, with a catalog, the agents an unattended Step may use (#1269).</summary>
/// <param name="profile">The approved profile.</param>
/// <param name="catalog">The launcher's agent catalog; null leaves RCP-AGENT-001 skipped.</param>
public sealed class ApprovedRecipeProfileFacts(IApprovedProfile profile, IAgentCatalog? catalog = null) : IRecipeProfileFacts
{
    /// <summary>The validator's profile facts for <paramref name="approved"/>: its effect allowlist and Step agents, or none.</summary>
    /// <param name="approved">The approved profile, or null when none is approved.</param>
    /// <returns>The facts RCP-EFFECT-001 and RCP-AGENT-001 read.</returns>
    private RecipeProfileFacts Of(LoadedProfile? approved) => approved is null
        ? RecipeProfileFacts.None
        : new RecipeProfileFacts(EffectSettings.Of(approved.Profile).Allowlist) { StepAgents = StepAgentsOf(approved) };

    // The catalog entries the Step approval check lets an unattended Step run (SL8); null without a catalog.
    private HashSet<string>? StepAgentsOf(LoadedProfile approved) => catalog is null
        ? null
        : [.. catalog.All.Where(e => StepApproval.Check(e, builtinArgvHash: null, approved.Profile.Agents?.StepArgvHashes(e.Id) ?? []).IsSuccess).Select(static e => e.Id.Value)];

    /// <inheritdoc/>
    public async ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken)
    {
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return approved.IsError ? approved.Error : Of(approved.Value);
    }
}
