using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.LinearIssues;

internal sealed class CreateLinearIssueService : IService<CreateLinearIssueRequest, CreateLinearIssueResponse>
{
    public ValueTask<Result<CreateLinearIssueResponse>> Handle(CreateLinearIssueRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
