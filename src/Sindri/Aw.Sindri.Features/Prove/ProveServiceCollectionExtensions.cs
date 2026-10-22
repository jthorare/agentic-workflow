using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Prove;

/// <summary>Registers the block runtimes of the prove station's Steps (line spec §7.3); the Steps' descriptors are registered with the blocks.</summary>
internal static class ProveServiceCollectionExtensions
{
    /// <summary>Adds the prove Steps' runtimes by block id.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddProveSlices(this IServiceCollection services) => services
        .AddBlockRuntime<ProveIntakeStep>()
        .AddBlockRuntime<ProveBeforeAfterStep>()
        .AddBlockRuntime<ProveEvidenceStep>()
        .AddBlockRuntime<ProveFailLogStep>()
        .AddBlockRuntime<RecheckPlanStep>()
        .AddBlockRuntime<ProveRerunScriptsStep>()
        .AddBlockRuntime<RecheckLogStep>();
}
