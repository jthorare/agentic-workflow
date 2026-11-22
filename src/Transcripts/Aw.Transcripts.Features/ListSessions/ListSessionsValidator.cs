using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Transcripts.Features.ListSessions;

internal sealed class ListSessionsValidator : IValidator<ListSessionsRequest>
{
    public Result<Unit> Validate(ListSessionsRequest value) => Unit.Value;
}
