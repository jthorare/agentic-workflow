using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The one recipe a Step run read finds its loops in; none when built empty (an unknown recipe).</summary>
/// <param name="recipe">The recipe, or null for none.</param>
internal sealed class StepRunRecipes(Recipe? recipe = null) : IApprovedRecipes
{
    public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => Found(id);

    public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => Found(id);

    public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion version, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(ApprovalCheck.Holds));

    private ValueTask<Result<ApprovedRecipe>> Found(RecipeId id) =>
        ValueTask.FromResult(recipe is null
            ? Results.Failure<ApprovedRecipe>(WorkflowErrors.RecipeNotFound.With("no such recipe"))
            : Results.Success(new ApprovedRecipe(new RecipeVersion(id, 1, RegistryData.Hash("step-run")), recipe)));
}
