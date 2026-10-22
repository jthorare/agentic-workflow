using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The confirmations (<see cref="NeedsYouConfirm"/>, continued): a changed workflow's typed approve (needs-you spec §8.3). The row names the recipe; the
/// version and hash come from GET /sindri/workflow/recipes, read again whenever a challenge is asked for. The challenge is 'approve &lt;recipe&gt; &lt;version&gt;',
/// its served text is what the box shows, and the answer goes to the recipe's own approve route with the hash read. Only a row whose subject is a recipe
/// is offered the box, and only when the catalog says ApproveRecipe is ready.
/// </summary>
internal sealed partial class NeedsYouConfirm
{
    private const string Standing = "approved";

    // A recipe row with a recipe subject is typed; any other row with ApproveRecipe stays the terminal copy.
    private static NeedsYouConfirmKind RecipeKind(NeedsYouRowViewModel row) => IsRecipe(row) ? NeedsYouConfirmKind.Typed : NeedsYouConfirmKind.Terminal;

    private static bool IsRecipe(NeedsYouRowViewModel row) =>
        row.Entry.Subject is { Id.Length: > 0 } subject && string.Equals(subject.Kind, NeedsYouKinds.Recipe, StringComparison.Ordinal) && string.Equals(row.Entry.PrimaryAction, RecipeAction, StringComparison.Ordinal);

    /// <summary>The version a changed workflow's approval is for: the one still waiting for approval, else the newest.</summary>
    /// <param name="recipes">The registered recipes.</param>
    /// <param name="id">The recipe id.</param>
    /// <returns>The version's row, or null when the recipe is not registered.</returns>
    internal static RecipeSummaryView? Pick(IReadOnlyList<RecipeSummaryView> recipes, string id)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        return recipes.Where(r => string.Equals(r.Id, id, StringComparison.Ordinal))
            .OrderBy(r => string.Equals(r.Standing, Standing, StringComparison.Ordinal) ? 1 : 0)
            .ThenByDescending(r => r.Version)
            .FirstOrDefault();
    }

    // The challenge of the shown row's kind.
    private Task Ask(int session) => _recipeShown ? AskRecipe(session) : AskProfile(session);

    // The answer of the shown row's kind, to its own route: the refusal, or whether it approved.
    private async Task<(Error? Refused, bool Approved)> Approve(Uri api, AppChallengeView challenge, string typed)
    {
        var answer = new ProfileAnswer(challenge.Challenge, typed);
        if (_recipeShown && _recipe is { } recipe)
        {
            var approved = await _api.ApproveRecipe(api, recipe.Id, recipe.Hash, answer, CancellationToken.None).ConfigureAwait(true);
            return (approved.IsError ? approved.Error : null, approved.IsSuccess);
        }

        var profile = await _api.ApproveProfile(api, new ApproveProfileRequest(_hash, answer), CancellationToken.None).ConfigureAwait(true);
        return (profile.IsError ? profile.Error : null, profile.IsSuccess && profile.Value.Approved);
    }

    // The version and hash as served now, then the challenge for 'approve <recipe> <version>'; its served text is what the box shows.
    private async Task AskRecipe(int session)
    {
        if (_url() is not { } api)
        {
            return;
        }

        var listed = await _api.Recipes(api, CancellationToken.None).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        _recipe = listed.IsError ? null : Pick(listed.Value.Recipes, _hash);
        if (_recipe is not { } recipe)
        {
            (_challenge, ProfileRefusal) = (null, listed.IsError ? Words(listed.Error) : Labels.NeedsYouRecipeGone);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        var action = "approve " + recipe.Id + " " + recipe.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var asked = await _api.IssueAppChallenge(api, new AppChallengeRequest(action), CancellationToken.None).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        if (asked.IsError)
        {
            (_challenge, ProfileRefusal) = (null, Words(asked.Error));
        }
        else
        {
            _challenge = asked.Value;
            TypedBox.Offer(asked.Value);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
