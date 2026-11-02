namespace Aw.Sindri.Core.Profile;

/// <summary>One state of one recipe, as far as a profile key naming a state needs it (model-pick spec §8).</summary>
/// <param name="Recipe">The recipe's id ('workflow.scoping').</param>
/// <param name="State">The state's name ('questions').</param>
public sealed record RecipeStateRef(string Recipe, string State);
