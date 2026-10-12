using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// What every <see cref="IChildRecipeResolver"/> does: the fake and each adapter (the same-directory reader, later the
/// registered recipes) bind it with a resolver that holds one known child and nothing else. A binding puts its lane on
/// the class.
/// </summary>
public abstract class ChildRecipeResolverContract
{
    /// <summary>A resolver that holds exactly one child recipe, and that child as the resolver should return it.</summary>
    /// <returns>The resolver under test and its known child.</returns>
    protected abstract ValueTask<(IChildRecipeResolver Resolver, ChildRecipe Known)> CreateWithKnown();

    /// <summary>A recipe the resolver holds resolves to that recipe and its hash.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_known_child_resolves_with_its_hash()
    {
        var (resolver, known) = await CreateWithKnown();

        var found = resolver.Resolve(known.Recipe.Id);

        Expect.True(found is not null);
        Expect.Equal(known.Recipe.Id, found!.Recipe.Id);   // why: checked non-null on the line above
        Expect.Equal(known.Hash, found.Hash);
    }

    /// <summary>A recipe the resolver doesn't hold is null, never an exception.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_child_is_null()
    {
        var (resolver, _) = await CreateWithKnown();

        Expect.True(resolver.Resolve(RecipeId.From("workflow.nowhere")) is null);
    }
}
