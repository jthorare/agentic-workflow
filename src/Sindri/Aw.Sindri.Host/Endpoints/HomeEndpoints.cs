using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.GetHomeRank;
using Aw.Sindri.Features.GetHomeRow;
using Aw.Sindri.Features.ListHome;
using Aw.Sindri.Features.ListHomeDone;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The Home reads (home spec §8): GET /sindri/home?split=&amp;after=&amp;limit=&amp;snoozed=, GET /sindri/home/rows/{id} and GET /sindri/home/rank/{id} (the ids percent-encoded), and GET /sindri/home/done. Each binds, calls one service and maps the result.
/// The writes (act, snooze, done, undo, mute) are later phases.
/// </summary>
internal static class HomeEndpoints
{
    internal static RouteGroupBuilder MapHomeReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/home", static async Task<IResult> (string? split, string? after, int? limit, string? snoozed, IService<ListHomeRequest, ListHomeResponse> service, CancellationToken ct) =>
        {
            var which = Split(split);
            if (which.IsError)
            {
                return which.ToHttpResult();
            }

            var request = new ListHomeRequest(which.Value, string.Equals(snoozed, "1", StringComparison.Ordinal) || string.Equals(snoozed, "true", StringComparison.Ordinal), string.IsNullOrEmpty(after) ? null : after, limit ?? 0);
            return (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapGet("/home/rows/{id}", static async Task<IResult> (string id, IService<GetHomeRowRequest, GetHomeRowResponse> service, CancellationToken ct) =>
        {
            var rowId = RowId(id);
            return rowId.IsError ? rowId.ToHttpResult() : (await service.Handle(new GetHomeRowRequest(rowId.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapGet("/home/done", static async Task<IResult> (IService<ListHomeDoneRequest, ListHomeDoneResponse> service, CancellationToken ct) =>
            (await service.Handle(new ListHomeDoneRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/home/rank/{id}", static async Task<IResult> (string id, IService<GetHomeRankRequest, GetHomeRankResponse> service, CancellationToken ct) =>
        {
            var rowId = RowId(id);
            return rowId.IsError ? rowId.ToHttpResult() : (await service.Handle(new GetHomeRankRequest(rowId.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }

    // why: ASP.NET keeps an escaped '/' as "%2F" in a route value (ProjectRoute.Segment has the same rule), and a gh: row id holds one, so the segment is unescaped here, once, for both routes
    private static Result<HomeRowId> RowId(string id) => HomeRowId.TryFrom(Uri.UnescapeDataString(id), out var rowId)
        ? rowId
        : SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(id), 80)}' isn't a Home row id (<source>:<id>, such as gh:pr:owner/repo#12)");

    private static Result<HomeGroup?> Split(string? split) => split switch
    {
        null or "" => (HomeGroup?)null,
        "needs-you" => HomeGroup.NeedsYou,
        "moving" => HomeGroup.Moving,
        _ => SindriErrors.InvalidInput.With($"split is needs-you or moving, not '{SafeText.Line(SafeText.Escape(split), 40)}' (fix: ?split=needs-you)"),
    };
}
