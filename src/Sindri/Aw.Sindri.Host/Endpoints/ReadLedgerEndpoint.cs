using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ReadLedger;

namespace Aw.Sindri.Host.Endpoints;

internal static class ReadLedgerEndpoint
{
    internal static RouteGroupBuilder MapReadLedger(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/ledger", static async Task<IResult> (long? after, string? item, string? type, int? max, string? fence, IService<ReadLedgerRequest, LedgerPage> service, ICallerContextAccessor callers, ITodoFencing fencing, CancellationToken ct) =>
        {
            ItemRef? itemRef = null;
            if (!string.IsNullOrEmpty(item))
            {
                var parsed = ItemRoute.Item(item);
                if (parsed.IsError)
                {
                    return parsed.ToHttpResult();
                }

                itemRef = parsed.Value;
            }

            var request = new ReadLedgerRequest(after ?? 0, itemRef, type, max ?? 100);
            var page = await service.Handle(request, ct).ConfigureAwait(false);
            return page.IsError || !TodoEndpoints.Fenced(callers, fence) || !page.Value.Events.Any(static e => e.Type.StartsWith("todo.", StringComparison.Ordinal)) ? page.ToHttpResult() : fencing.Ledger(page.Value);   // a todo event's text is fenced for a session and for a piped CLI (todo spec §4.4)
        });
        return group;
    }
}
