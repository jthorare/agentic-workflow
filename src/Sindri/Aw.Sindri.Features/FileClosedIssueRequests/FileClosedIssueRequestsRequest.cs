using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.FileClosedIssueRequests;

/// <summary>The pull side after a 'linear' tick (Linear hub spec 2026-11-04 §6.3; a service caller): files 'request.filed' for each open item whose linked issue became completed or canceled. Its request id is derived from (item, issue key, completedAt or canceledAt), so a rerun files nothing twice.</summary>
internal sealed record FileClosedIssueRequestsRequest : ICommand<FileClosedIssueRequestsResponse>;
