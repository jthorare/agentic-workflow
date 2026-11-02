using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetLaunchPicks;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/launch/picks[?item=&amp;instance=] (model-pick spec §6.1; query): the model picks of the item's or the instance's
/// runs, oldest first, each with its tokens, priced cost, outcome word and the plain sentence for why. A malformed item or instance
/// is SND-CLI-003.
/// </summary>
internal static class GetLaunchPicksEndpoint
{
    internal static RouteGroupBuilder MapGetLaunchPicks(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/launch/picks", static async (string? item, string? instance, IService<GetLaunchPicksRequest, LaunchPicks> service, CancellationToken ct) =>
        {
            var itemRef = ItemRoute.ItemFilter(item);
            var address = Instance(instance);
            if (itemRef.IsError)
            {
                return itemRef.ToHttpResult();
            }

            if (address.IsError)
            {
                return address.ToHttpResult();
            }

            return (await service.Handle(new GetLaunchPicksRequest(itemRef.Value, address.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }

    private static Kernel.Result<InstanceAddress?> Instance(string? instance)
    {
        if (string.IsNullOrEmpty(instance))
        {
            return (InstanceAddress?)null;
        }

        return InstanceAddress.TryFrom(instance, out var address)
            ? address
            : SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(instance), 80)}' isn't an instance address (fix: ?instance=T-1/step/1)");
    }
}
