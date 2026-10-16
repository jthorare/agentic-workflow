using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Profile;

/// <summary>The recipe validator's profile facts (spec §9.1, S1-D11) read from the approved profile: its effect allowlist.</summary>
/// <param name="profile">The approved profile.</param>
public sealed class ApprovedRecipeProfileFacts(IApprovedProfile profile) : IRecipeProfileFacts
{
    /// <summary>The validator's profile facts for <paramref name="approved"/>: its effect allowlist, or none.</summary>
    /// <param name="approved">The approved profile, or null when none is approved.</param>
    /// <returns>The facts RCP-EFFECT-001 reads.</returns>
    private static RecipeProfileFacts Of(LoadedProfile? approved) =>
        approved is null ? RecipeProfileFacts.None : new RecipeProfileFacts(EffectSettings.Of(approved.Profile).Allowlist);

    /// <inheritdoc/>
    public async ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken)
    {
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return approved.IsError ? approved.Error : Of(approved.Value);
    }
}
