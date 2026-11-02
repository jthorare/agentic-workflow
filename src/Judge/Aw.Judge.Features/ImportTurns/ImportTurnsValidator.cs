using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ImportTurns;

internal sealed class ImportTurnsValidator : IValidator<ImportTurnsRequest>
{
    public Result<Unit> Validate(ImportTurnsRequest value) => Unit.Value;
}
