using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/linear/issues/{key} (Linear hub spec §3; query): one stored issue with its description and newest comments. Never stored, or outside a session's teams, is SND-LINEAR-005 (404). A session caller's Linear text is inside the untrusted fence; the human gets it plain.</summary>
internal static class GetLinearIssueEndpoint
{
    internal static RouteGroupBuilder MapGetLinearIssue(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/linear/issues/{key}", static async Task<IResult> (string key, IService<GetLinearIssueRequest, GetLinearIssueResponse> service, ICallerContextAccessor callers, ILinearIssueFencing fencing, CancellationToken ct) =>
        {
            var issue = await service.Handle(new GetLinearIssueRequest(key), ct).ConfigureAwait(false);
            return issue.IsError || callers.Current?.Class != CallerClass.Session ? issue.ToHttpResult() : fencing.One(issue.Value);
        });
        return group;
    }
}
