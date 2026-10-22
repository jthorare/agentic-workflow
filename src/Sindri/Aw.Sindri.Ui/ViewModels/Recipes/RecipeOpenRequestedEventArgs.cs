using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>A workflow row asks the shell to open its sheet.</summary>
/// <param name="link">The recipe's link.</param>
internal sealed class RecipeOpenRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>The recipe's link.</summary>
    public DeepLink Link { get; } = link;
}
