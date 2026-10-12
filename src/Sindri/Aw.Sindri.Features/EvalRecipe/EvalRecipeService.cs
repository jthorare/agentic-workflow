using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>Folds the engine's decisions over each eval case's triggers and reports the first line that diverges. Shell: P3 unit (f) implements it.</summary>
internal sealed class EvalRecipeService : IService<EvalRecipeRequest, EvalRecipeResponse>
{
    public ValueTask<Result<EvalRecipeResponse>> Handle(EvalRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("recipe eval lands with P3");
}
