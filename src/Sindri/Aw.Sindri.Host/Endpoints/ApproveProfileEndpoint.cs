using Aw.Sindri.Core;

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

    private static IResult Removed() => HttpResults.Problem(SindriErrors.RouteRemoved.With(RemovedText), StatusCodes.Status410Gone);
}
