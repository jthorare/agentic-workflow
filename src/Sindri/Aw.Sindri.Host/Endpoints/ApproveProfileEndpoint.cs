using Aw.Sindri.Core;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The removed 'POST /sindri/profile/approve' (#2258 PR 1): answers SND-API-014 with status 410 for one release, so an old CLI or script reads why it stopped working.</summary>
internal static class ApproveProfileEndpoint
{
    internal const string RemovedText = "removed: edits apply on save";

    internal static RouteGroupBuilder MapApproveProfile(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/profile/approve", Removed);
        return group;
    }

    // TypedResults directly, not HttpResults: this folder must not depend on the root namespace (ARCH-CYCLE-001), and the error carries no extensions.
    private static ProblemHttpResult Removed() => TypedResults.Problem(title: SindriErrors.RouteRemoved.Code, detail: RemovedText, statusCode: StatusCodes.Status410Gone);
}
