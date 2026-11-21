using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.GetHomeRow;
using Aw.Sindri.Features.ListHome;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The Home reads (home spec §8): GET /sindri/home?split=&amp;after=&amp;limit=&amp;snoozed= and GET /sindri/home/rows/{id}, the id percent-encoded. Each binds, calls one service and maps the result.
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

            var request = new ListHomeRequest(which.Value, snoozed is "1" or "true", string.IsNullOrEmpty(after) ? null : after, limit ?? 0);
            return (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapGet("/home/rows/{id}", static async Task<IResult> (string id, IService<GetHomeRowRequest, GetHomeRowResponse> service, CancellationToken ct) =>
        {
            if (!HomeRowId.TryFrom(id, out var rowId))
            {
                Result<Unit> bad = SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(id), 80)}' isn't a Home row id (<source>:<id>, such as gh:pr:owner/repo#12)");
                return bad.ToHttpResult();
            }

            return (await service.Handle(new GetHomeRowRequest(rowId), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }

    private static Result<HomeGroup?> Split(string? split) => split switch
    {
        null or "" => (HomeGroup?)null,
        "needs-you" => HomeGroup.NeedsYou,
        "moving" => HomeGroup.Moving,
        _ => SindriErrors.InvalidInput.With($"split is needs-you or moving, not '{SafeText.Line(SafeText.Escape(split), 40)}' (fix: ?split=needs-you)"),
    };
}
