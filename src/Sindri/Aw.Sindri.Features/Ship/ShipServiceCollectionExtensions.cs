using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Ship;

/// <summary>Registers the block runtimes of the ship station's Steps (line spec §7.5).</summary>
internal static class ShipServiceCollectionExtensions
{
    /// <summary>Adds the ship Steps' runtimes by block id; their descriptors are registered with the blocks.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddShipSlices(this IServiceCollection services) => services
        .AddBlockRuntime<ShipReturnStep>();
}
