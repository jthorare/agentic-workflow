using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.CodeIndex.Features;

/// <summary>Registers Aw.CodeIndex.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class CodeIndexFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every CodeIndex use case as an IService, and the facade over them. The host registers the ports (IRepoHeads, IHeavyJobLock, TimeProvider) and Infrastructure registers the rest.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddCodeIndexFeatures(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddSingleton<StructureWriter>()
            .AddSingleton<EmbeddingsLayer>()
            .AddSingleton<GraphLayer>()
            .AddSingleton<BuildLayers>()
            .AddSingleton<IService<BuildCodeIndexRequest, BuildReport>, BuildCodeIndexService>()
            .AddSingleton<ICodeIndex, CodeIndexFacade>();
    }
}
