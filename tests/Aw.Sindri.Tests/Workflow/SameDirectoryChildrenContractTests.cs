using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The same-directory adapter against the resolver contract: workflow.item finds workflow.step beside it.</summary>
[Lane(TestLane.Unit)]
public sealed class SameDirectoryChildrenContractTests : ChildRecipeResolverContract
{
    protected override async ValueTask<(IChildRecipeResolver Resolver, ChildRecipe Known)> CreateWithKnown()
    {
        var files = new InMemoryRecipeFiles();
        foreach (var name in new[] { "workflow.item", "workflow.step" })
        {
            _ = files.With($"/recipes/{name}.yaml", ShippedRecipes.Read($"sindri/{name}.yaml"));
        }

        var parent = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.item.yaml"), "workflow.item.yaml").Document!;
        var child = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml").Document!;
        var resolver = await SameDirectoryChildren.Load("/recipes/workflow.item.yaml", parent.Recipe, RecipeReaderTests.Reader, files, TestContext.Current.CancellationToken);
        return (resolver, new ChildRecipe(child.Recipe, child.Hash));
    }
}
