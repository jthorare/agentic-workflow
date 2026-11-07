using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Edits a stored Linear issue (Linear hub spec 2026-11-04 §3, §5.3, D6). Red: the behavior is not written yet.</summary>
internal sealed class ChangeLinearIssueService : IService<ChangeLinearIssueRequest, LinearChangeResult>
{
    public ValueTask<Result<LinearChangeResult>> Handle(ChangeLinearIssueRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
