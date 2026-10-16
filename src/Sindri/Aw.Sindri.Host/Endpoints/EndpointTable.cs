namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Maps every Sindri endpoint under /sindri (factory addendum §17.21 API): each binds, calls one service and maps its Result.
/// Queries are GETs and commands POSTs (ARCH-CQRS-001); each slice adds one line under its group's anchor (API spec C7).
/// </summary>
internal static class EndpointTable
{
    internal static WebApplication MapSindriEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sindri");
        var queries = group.MapGroup(string.Empty).WithMetadata(QueryRouteMetadata.Instance);   // X-Sindri-After honoured (read-your-writes)
        var commands = group.MapGroup(string.Empty).WithMetadata(CommandRouteMetadata.Instance);   // Origin refused before binding (SND-API-008)

        _ = MapQueries(queries);
        _ = MapCommands(commands);
        return app;
    }

    private static RouteGroupBuilder MapQueries(RouteGroupBuilder queries)
    {
        // ---- queries: one line per slice, sorted ----
        _ = queries.MapGetEventCatalog();
        _ = queries.MapGetItem();
        _ = queries.MapGetItemTimeline();
        _ = queries.MapGetQueue();
        _ = queries.MapGetNeedsYou();
        _ = queries.MapListDecisions();
        _ = queries.MapListRequests();
        _ = queries.MapGetInstance();
        _ = queries.MapGetInstanceEvents();
        _ = queries.MapGetProfile();
        _ = queries.MapExportContextMap();
        _ = queries.MapExportContextReview();
        _ = queries.MapFindContextPath();
        _ = queries.MapGetContextFileMap();
        _ = queries.MapGetContextLenses();
        _ = queries.MapGetContextMap();
        _ = queries.MapGetContextProposals();
        _ = queries.MapGetContextReview();
        _ = queries.MapGetContextReviewHistory();
        _ = queries.MapGetContextSources();
        _ = queries.MapGetStatus();
        _ = queries.MapListContextNeighbors();
        _ = queries.MapListCommands();
        _ = queries.MapListItems();
        _ = queries.MapListInstances();
        _ = queries.MapListOutbox();
        _ = queries.MapListServices();
        _ = queries.MapListRecipes();
        _ = queries.MapPingProcess();
        _ = queries.MapQueryContext();
        _ = queries.MapReadLedger();
        _ = queries.MapShowContextNode();
        _ = queries.MapShowRecipe();
        _ = queries.MapStreamEvents();
        return queries;
    }

    private static RouteGroupBuilder MapCommands(RouteGroupBuilder commands)
    {
        // ---- commands: one line per slice, sorted ----
        _ = commands.MapApproveProfile();
        _ = commands.MapCloseItem();
        _ = commands.MapCreateItem();
        _ = commands.MapDecide();
        _ = commands.MapFileRequest();
        _ = commands.MapRebuildViews();
        _ = commands.MapImportContextMap();
        _ = commands.MapPromoteContextMap();
        _ = commands.MapProposeContextMap();
        _ = commands.MapReviewContextMap();
        _ = commands.MapStopProcess();
        _ = commands.MapApproveRecipe();
        _ = commands.MapRevokeRecipe();
        _ = commands.MapIssueChallenge();
        _ = commands.MapRegisterRecipe();
        _ = commands.MapStartInstance();
        _ = commands.MapIssueNonce();
        _ = commands.MapApplyCommand();
        _ = commands.MapCompleteStep();
        return commands;
    }
}
