using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.LinearIssues;

internal sealed class CommentLinearIssueService : IService<CommentLinearIssueRequest, CommentLinearIssueResponse>
{
    public ValueTask<Result<CommentLinearIssueResponse>> Handle(CommentLinearIssueRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
