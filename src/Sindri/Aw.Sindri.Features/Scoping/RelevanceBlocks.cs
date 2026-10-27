using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Scoping;

/// <summary>Registers the relevance review's three Steps: the block runtimes (the descriptors are registered with the catalog, in <c language="text">BlockRegistrations</c>) of the judge and the apply Step, and the review agent's instructions.</summary>
internal static class RelevanceBlocks
{
    /// <summary>Adds the runtimes and the instructions.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddScopeRelevance(this IServiceCollection services) => services
        .AddBlockRuntime<RelevanceJudgeStep>()
        .AddBlockRuntime<RelevanceApplyStep>()
        .AddSingleton<IAgentStepInstructions, RelevanceReviewInstructions>();
}
