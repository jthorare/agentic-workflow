using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Review;

/// <summary>Registers the block runtime of the review result's Step (line spec §7.4) and what the review fix Step is told (#1682); descriptors are registered with the blocks.</summary>
internal static class ReviewServiceCollectionExtensions
{
    /// <summary>Adds the review record Step's runtime by block id and the review fix Step's instructions.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddReviewSlices(this IServiceCollection services) => services
        .AddSingleton<IAgentStepInstructions, ReviewFixInstructions>()
        .AddBlockRuntime<ReviewRecordStep>();
}
