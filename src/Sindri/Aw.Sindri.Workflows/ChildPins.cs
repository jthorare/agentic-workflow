using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The child recipes an instance's approval pinned (engine P7 plan D2): what the interpreter creates children from.</summary>
/// <param name="loaded">The pinned children with their grandchildren, as 'LoadChildren' read them.</param>
internal sealed class ChildPins(IReadOnlyList<LoadedChild> loaded)
{
    /// <summary>No pinned child: a history from before engine P7, or a recipe with no child wait.</summary>
    public static ChildPins None { get; } = new([]);

    /// <summary>The resolver that hands a child out only at the hash the approval pinned.</summary>
    /// <returns>The resolver.</returns>
    public IChildRecipeResolver Resolver() => loaded.Count == 0
        ? NoChildRecipes.Instance
        : new PinnedChildRecipes(loaded.ToDictionary(static c => c.Id, static c => new ChildRecipe(c.Recipe, c.Hash)), loaded.ToDictionary(static c => c.Id, static c => c.Hash));

    /// <summary>The pinned hash of each child <paramref name="recipe"/> waits on.</summary>
    /// <param name="recipe">The instance's recipe.</param>
    /// <returns>The pins by child recipe id.</returns>
    public IReadOnlyDictionary<RecipeId, RecipeHash> Of(Recipe recipe) =>
        RecipeChildren.Of(recipe).Where(id => loaded.Any(c => c.Id == id)).ToDictionary(static id => id, id => loaded.First(c => c.Id == id).Hash);
}
