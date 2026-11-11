using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.ReportDefect;

internal sealed class ReportDefectValidator : IValidator<ReportDefectRequest>
{
    public Result<Unit> Validate(ReportDefectRequest value) => throw new NotSupportedException();
}
