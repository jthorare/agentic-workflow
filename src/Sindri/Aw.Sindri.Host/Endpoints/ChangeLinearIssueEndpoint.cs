using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Features.LinearIssues;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/linear/issues/{key}/change (Linear hub spec 2026-11-04 §3, §5.3, D6; command, owner only): edits a stored Linear issue. 200 is the edit with the re-read issue; a field Linear changed since the sheet opened is
/// 409 SND-LINEAR-409 with the differing fields and Linear's issue, and nothing was sent. The 409's members are written as they are, not cleaned like an error's extensions: they carry the issue's own text, which this owner-only route
/// returns plain, as GET /sindri/linear/issues/{key} does. Every other ending is an error.
/// </summary>
internal static class ChangeLinearIssueEndpoint
{
    internal static RouteGroupBuilder MapChangeLinearIssue(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/linear/issues/{key}/change", static async (string key, ChangeLinearIssueBody body, IService<ChangeLinearIssueRequest, LinearChangeResult> service, HttpContext http, CancellationToken ct) =>
        {
            var result = await service.Handle(body.ToRequest(key), ct).ConfigureAwait(false);
            return result.IsError ? result.ToHttpResult()
                : result.Value.Conflict is { } conflict ? Conflict(conflict, http.RequestServices.GetRequiredService<JsonSerializerOptions>())
                : TypedResults.Ok(result.Value.Applied);
        });
        return group;
    }

    /// <remarks>The options come from the request's services, not SindriJsonContext, so Endpoints never depends on the host namespace (ARCH-CYCLE-001).</remarks>
    private static ProblemHttpResult Conflict(LinearChangeConflict conflict, JsonSerializerOptions json) => TypedResults.Problem(
        title: "SND-LINEAR-409",
        detail: $"{conflict.Issue.Key} changed in Linear at {conflict.ChangedAt.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture)}; nothing was sent (fix: review the fields below, then save yours anyway or take Linear's)",
        statusCode: StatusCodes.Status409Conflict,
        extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["changedAt"] = conflict.ChangedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture),
            ["fields"] = JsonSerializer.SerializeToElement((LinearFieldConflict[])[.. conflict.Fields], json.GetTypeInfo(typeof(LinearFieldConflict[]))),
            ["issue"] = JsonSerializer.SerializeToElement(conflict.Issue, json.GetTypeInfo(typeof(GetLinearIssueResponse))),
        });
}
