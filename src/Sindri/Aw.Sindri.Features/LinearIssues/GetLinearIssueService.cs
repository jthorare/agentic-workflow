using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>One stored issue (Linear hub spec §3). Never stored, or outside the caller's audience, is SND-LINEAR-005. Names, link and PR come from the same facts as the list.</summary>
/// <param name="facts">The stored issues and the viewer.</param>
/// <param name="links">The items and pull requests linked to issues.</param>
/// <param name="gate">Whether the caller may write.</param>
internal sealed class GetLinearIssueService(ILinearIssueFacts facts, ILinearItemLinks links, ILinearWriteGate gate) : IService<GetLinearIssueRequest, GetLinearIssueResponse>
{
    public ValueTask<Result<GetLinearIssueResponse>> Handle(GetLinearIssueRequest request, CancellationToken cancellationToken) => throw new NotSupportedException($"{facts}{links}{gate}");
}
