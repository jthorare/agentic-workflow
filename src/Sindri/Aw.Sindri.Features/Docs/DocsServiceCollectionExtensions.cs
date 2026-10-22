using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Docs;

/// <summary>Registers the block runtimes of the docs-sync station's Steps (docs-sync spec §7).</summary>
internal static class DocsServiceCollectionExtensions
{
    /// <summary>Adds the docs Steps' runtimes by block id; their descriptors are registered with the blocks.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddDocsSlices(this IServiceCollection services) => services
        .AddBlockRuntime<DocsCheckStep>()
        .AddBlockRuntime<DocsScopeStep>()
        .AddScoped<IService<DocsCheckRequest, DocsCheckResponse>, DocsCheckService>();
}
