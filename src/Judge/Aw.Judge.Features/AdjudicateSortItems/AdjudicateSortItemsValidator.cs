using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AdjudicateSortItems;

internal sealed class AdjudicateSortItemsValidator : IValidator<AdjudicateSortItemsRequest>
{
    public Result<Unit> Validate(AdjudicateSortItemsRequest value) => Unit.Value;
}
