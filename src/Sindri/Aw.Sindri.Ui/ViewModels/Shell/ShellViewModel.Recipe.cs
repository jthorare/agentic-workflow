using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The recipe sheet in the shell (<see cref="ShellViewModel"/>, continued; U10a): a recipe link opens it, read by id and by hash when the link names one.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The recipe sheet.</summary>
    public RecipeSheetViewModel Recipe { get; }

    /// <summary>The Recipes page's list (shown to people as workflows).</summary>
    public RecipesPageViewModel RecipesPage { get; private set; }

    /// <summary>The Workflows group whose Recipe button opened the sheet (its recipe id), where Esc returns focus; null when it was opened some other way.</summary>
    internal string? OpenerRecipeGroup { get; private set; }

    /// <summary>True when the sheet's subject is a recipe.</summary>
    public bool IsRecipeSheet => SheetLink?.Kind == DeepLinkKind.Recipe;

    // The recipe part of opening the sheet (OnSheetOpened): the sheet names its recipe at once and reads it.
    private void OnRecipeSheetOpened(DeepLink link)
    {
        OpenerRecipeGroup = null;   // set again by the group head that opened this one, after the sheet opens
        Changed(nameof(IsRecipeSheet), nameof(ShownSheetWidth));
        if (link.Kind != DeepLinkKind.Recipe || link.Subject is not { } subject)
        {
            return;
        }

        Recipe.Show(subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = Recipe.Open(url, subject, CancellationToken.None);
        }
    }

    [MemberNotNull(nameof(RecipesPage))]
    private void InitRecipe()
    {
        RecipesPage = new RecipesPageViewModel(_api);
        Health.Follow(RecipesPage.Refresh);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsRecipes), StringComparison.Ordinal))
            {
                RecipesPage.SetShown(IsRecipes);
            }
        };
        RecipesPage.OpenRequested += (_, e) => OpenSheet(e.Link);
        RecipesPage.ApproveNewRequested += (_, _) => Navigate(DeepLink.Setup);   // Setup's held approve-all: the app approves nothing from here
        Health.Follow((url, token) => IsRecipeSheet ? Recipe.Refresh(url, token) : Task.CompletedTask);

        // The frame's link stacks the recipe on it (Back returns to the frame); a group head's button opens the approved version over the page it is on.
        Instance.RecipeRequested += (_, e) =>
        {
            Stacked(() => OpenSheet(new DeepLink(DeepLinkKind.Recipe, e.Subject)), push: true);
            SheetStacked?.Invoke(this, EventArgs.Empty);   // the Recipe button stays under the sheet: focus goes to the sheet's heading
        };
        Workflows.RecipeRequested += (_, e) =>
        {
            OpenSheet(new DeepLink(DeepLinkKind.Recipe, e.Subject));
            OpenerRecipeGroup = e.Subject;   // Esc comes back to this group's button
        };
    }

    // The Recipes page's own keys (J / K, Enter) belong to the page and never to the sheet.
    private bool RecipesKeyPressed(string key, bool inSheet) => !inSheet && IsRecipes && RecipesPage.Press(key);
}
