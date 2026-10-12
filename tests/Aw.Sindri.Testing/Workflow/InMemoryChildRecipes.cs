using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IChildRecipeResolver"/>: the child recipes a test gives it, by id.</summary>
public sealed class InMemoryChildRecipes : IChildRecipeResolver
{
    private readonly Dictionary<RecipeId, ChildRecipe> _byId = [];

    /// <summary>No child recipes.</summary>
    public static InMemoryChildRecipes None => new();

    /// <summary>Adds (or replaces) <paramref name="child"/>.</summary>
    /// <param name="child">A child recipe.</param>
    /// <returns>This resolver.</returns>
    public InMemoryChildRecipes With(ChildRecipe child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _byId[child.Recipe.Id] = child;
        return this;
    }

    /// <inheritdoc/>
    public ChildRecipe? Resolve(RecipeId id) => _byId.GetValueOrDefault(id);
}
