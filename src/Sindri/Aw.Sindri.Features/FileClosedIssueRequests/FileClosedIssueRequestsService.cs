using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.FileClosedIssueRequests;

/// <summary>Red stub for Linear hub Ph4's pull-side request (spec §6.3); the ports arrive with the implementation.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub: registered when the implementation lands; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FileClosedIssueRequestsService : IService<FileClosedIssueRequestsRequest, FileClosedIssueRequestsResponse>
{
    public ValueTask<Result<FileClosedIssueRequestsResponse>> Handle(FileClosedIssueRequestsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException("red: FileClosedIssueRequestsService");
}
