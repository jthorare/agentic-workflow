using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Pr;

/// <summary>Registers the PR babysitter's Step runtimes (babysitter spec §10.1) and what its agent Steps are told (Appendix A).</summary>
internal static class PrServiceCollectionExtensions
{
    /// <summary>Adds the PR Steps' runtimes by block id (their descriptors are registered with the blocks) and the agent Steps' instructions.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPrSlices(this IServiceCollection services) => services
        .AddSingleton<IAgentStepInstructions, CiFixInstructions>()
        .AddBlockRuntime<PrIntakeStep>()
        .AddBlockRuntime<PrSnapshotStep>()
        .AddBlockRuntime<PrNextCommentStep>()
        .AddBlockRuntime<PrHeldCommentStep>()
        .AddBlockRuntime<PrReadyCheckStep>();
}
