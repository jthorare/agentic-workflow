using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Registers the block descriptors this build loads (the skill blocks).</summary>
public static class BlockServiceCollectionExtensions
{
    /// <summary>Registers a block descriptor; the <see cref="BlockCatalog"/> collects them.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBlockDescriptor(this IServiceCollection services, BlockDescriptor descriptor) => services.AddSingleton(descriptor);
}
