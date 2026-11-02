using System.Globalization;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.ListDenials;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/sandbox/denials?launch=&amp;since=&amp;item= (sandbox spec §3.6): the denial counters, grouped for the owner by the CLI and the app.</summary>
internal static class ListDenialsEndpoint
{
    internal static RouteGroupBuilder MapListDenials(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sandbox/denials", static async (string? launch, string? since, string? item, IService<ListDenialsRequest, ListDenialsResponse> service, CancellationToken ct) =>
        {
            LaunchId? id = null;
            if (!string.IsNullOrEmpty(launch))
            {
                var parsed = LaunchRoute.Launch(launch);
                if (parsed.IsError)
                {
                    return parsed.ToHttpResult();
                }

                id = parsed.Value;
            }

            DateTimeOffset? from = null;
            if (!string.IsNullOrEmpty(since))
            {
                if (!DateTimeOffset.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
                {
                    return HttpResults.Problem(SindriErrors.InvalidInput.With($"since must be a time such as 2026-10-27T09:00:00Z, not '{since}'"));
                }

                from = at;
            }

            ItemId? forItem = null;
            if (!string.IsNullOrEmpty(item))
            {
                if (!ItemId.TryFrom(item, out var parsedItem))
                {
                    return HttpResults.Problem(SindriErrors.InvalidInput.With($"item must be an item id such as ITM-0194f2a6c8b17e3fa1d09c5b2e4f6a80, not '{item}'"));
                }

                forItem = parsedItem;
            }

            return (await service.Handle(new ListDenialsRequest(id, from, forItem), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
