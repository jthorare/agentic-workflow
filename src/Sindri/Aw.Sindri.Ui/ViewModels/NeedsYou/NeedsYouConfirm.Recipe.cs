using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The approvals (<see cref="NeedsYouConfirm"/>, continued): a changed workflow's approve (needs-you spec §8.3). The row names the recipe; the
/// version and hash come from GET /sindri/workflow/recipes, read again at the click, and the approval goes to the recipe's own approve route with the
/// hash read. Only a row whose subject is a recipe is offered it.
/// </summary>
internal sealed partial class NeedsYouConfirm
{
    private const string Standing = "approved";

    private static bool IsRecipe(NeedsYouRowViewModel row) =>
        row.Entry.Subject is { Id.Length: > 0 } subject && string.Equals(subject.Kind, NeedsYouKinds.Recipe, StringComparison.Ordinal) && string.Equals(row.Entry.PrimaryAction, RecipeAction, StringComparison.Ordinal);

    /// <summary>The hash a row's served command names ("sindri recipe approve &lt;id&gt;@&lt;hash&gt;"); empty when it names none.</summary>
    /// <param name="cli">The row's served command.</param>
    /// <returns>The hash, or empty.</returns>
    internal static string HashOf(string? cli)
    {
        var at = cli?.LastIndexOf('@') ?? -1;   // a char search is ordinal
        return at < 0 ? "" : cli![(at + 1)..].Trim();   // why: at is only >= 0 when cli is not null
    }

    /// <summary>The version a changed workflow's approval is for: the one the row names by hash, else the waiting one (it gained a capability, or its standing is not approved), else the newest.</summary>
    /// <param name="recipes">The listed recipes, a waiting shipped version as a row of its own.</param>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash the row's command names; empty or null for none.</param>
    /// <returns>The version's row, or null when the recipe is not listed.</returns>
    internal static RecipeSummaryView? Pick(IReadOnlyList<RecipeSummaryView> recipes, string id, string? hash = null)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        var own = recipes.Where(r => string.Equals(r.Id, id, StringComparison.Ordinal)).ToList();
        return (hash is { Length: > 0 } ? own.FirstOrDefault(r => string.Equals(r.Hash, hash, StringComparison.Ordinal)) : null)
            ?? own.Where(r => r.GainsCapability == true).OrderByDescending(r => r.Version).FirstOrDefault()
            ?? own.OrderBy(r => string.Equals(r.Standing, Standing, StringComparison.Ordinal) ? 1 : 0).ThenByDescending(r => r.Version).FirstOrDefault();
    }

    // The approval of the shown row's kind, to its own route: the refusal, or whether it approved. A workflow is approved at the version and hash served now.
    private async Task<(Error? Refused, bool Approved)> Send(Uri api)
    {
        if (Kind == NeedsYouConfirmKind.Recipe)
        {
            var listed = await _api.Recipes(api, CancellationToken.None).ConfigureAwait(true);
            if (listed.IsError)
            {
                return (listed.Error, false);
            }

            if (Pick(listed.Value.Recipes, _hash, HashOf(_cli)) is not { } recipe)
            {
                return (new Error("SND-UI-004", ErrorKind.NotFound, Labels.NeedsYouRecipeGone), false);
            }

            var approved = await _api.ApproveRecipe(api, recipe.Id, recipe.Hash, CancellationToken.None).ConfigureAwait(true);
            return (approved.IsError ? approved.Error : null, approved.IsSuccess);
        }

        var profile = await _api.ApproveProfile(api, new ApproveProfileRequest(_hash), CancellationToken.None).ConfigureAwait(true);
        return (profile.IsError ? profile.Error : null, profile.IsSuccess && profile.Value.Approved);
    }
}
