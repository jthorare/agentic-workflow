using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryChildRecipesContractTests : ChildRecipeResolverContract
{
    protected override ValueTask<(IChildRecipeResolver Resolver, ChildRecipe Known)> CreateWithKnown()
    {
        var known = new ChildRecipe(ARecipe.Minimal(RecipeId.From("workflow.child")), RecipeHash.From("sha256:" + new string('c', 64)));
        return ValueTask.FromResult<(IChildRecipeResolver, ChildRecipe)>((new InMemoryChildRecipes().With(known), known));
    }
}
