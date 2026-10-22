namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>One figure on the recipe sheet's "How it has run": a label and its value.</summary>
/// <param name="Label">What the figure is.</param>
/// <param name="Value">The figure in words.</param>
internal sealed record RecipeStatLine(string Label, string Value);
