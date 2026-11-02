using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ImportSortItems;

internal sealed class ImportSortItemsValidator : IValidator<ImportSortItemsRequest>
{
    public Result<Unit> Validate(ImportSortItemsRequest value) => Unit.Value;
}
