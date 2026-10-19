using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>Registers the block runtimes of the lens loops' Steps (line spec §4.1, §7.4).</summary>
internal static class LensLoopsServiceCollectionExtensions
{
    /// <summary>Adds the lens log Step; their descriptors are registered with the catalog (BlockRegistrations).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLensLoops(this IServiceCollection services) => services
        .AddBlockRuntime<LensLogStep>();
}
