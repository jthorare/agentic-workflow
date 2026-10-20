using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Build;

/// <summary>Registers the block runtimes of the build station's Steps and the phase's drift check (line spec §7.1, §7.2).</summary>
internal static class BuildServiceCollectionExtensions
{
    /// <summary>Adds the build Steps' runtimes by block id; their descriptors are registered with the blocks.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddBuildSlices(this IServiceCollection services) => services
        .AddBlockRuntime<BuildPrepareStep>()
        .AddBlockRuntime<PhaseDriftStep>();
}
