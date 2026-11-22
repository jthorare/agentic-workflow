using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Transcripts.Core;

namespace Aw.Transcripts.Features.ListSessions;

internal sealed class ListSessionsValidator : IValidator<ListSessionsRequest>
{
    public Result<Unit> Validate(ListSessionsRequest value) =>
        value.Repo is { } repo && (repo.Trim().Length == 0 || !repo.StartsWith('/'))
            ? TranscriptsErrors.Invalid.With("repo must be an absolute path")
            : Unit.Value;
}
