using Aw.Kernel.Services;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The Linear hub's comment and create writes (Linear hub spec §5.2, §5.3): POST /sindri/linear/issues/{key}/comments and POST /sindri/linear/issues (201). Each binds, calls one service and maps the result.</summary>
internal static class LinearCommentCreateEndpoints
{
    internal static RouteGroupBuilder MapLinearCommentCreate(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/linear/issues/{key}/comments", static async (string key, CommentLinearIssueBody body, IService<CommentLinearIssueRequest, CommentLinearIssueResponse> service, CancellationToken ct)
            => (await service.Handle(body.ToRequest(key), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/linear/issues", static async (CreateLinearIssueRequest request, IService<CreateLinearIssueRequest, CreateLinearIssueResponse> service, CancellationToken ct) =>
        {
            var created = await service.Handle(request, ct).ConfigureAwait(false);
            return created.IsError ? created.ToHttpResult() : TypedResults.Created("/sindri/linear/issues/" + created.Value.Issue.Key, created.Value);
        });
        return group;
    }
}
