using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ImportLabels;

internal sealed class ImportLabelsValidator : IValidator<ImportLabelsRequest>
{
    public Result<Unit> Validate(ImportLabelsRequest value) => Unit.Value;
}
