using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Evidence;

/// <summary>Registers the block runtimes of the evidence blocks' pure Steps (decision-tree spec §5).</summary>
internal static class EvidenceServiceCollectionExtensions
{
    /// <summary>Adds the evidence Steps' runtimes by block id; their descriptors are registered with the blocks.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddEvidenceSlices(this IServiceCollection services) => services
        .AddBlockRuntime<EvidenceNoneStep>()
        .AddBlockRuntime<EvidenceCombineStep>()
        .AddEvidenceInstructions();

    private static IServiceCollection AddEvidenceInstructions(this IServiceCollection services)
    {
        foreach (var instructions in EvidenceStepInstructions.All)
        {
            _ = services.AddSingleton<IAgentStepInstructions>(instructions);
        }

        return services;
    }
}
