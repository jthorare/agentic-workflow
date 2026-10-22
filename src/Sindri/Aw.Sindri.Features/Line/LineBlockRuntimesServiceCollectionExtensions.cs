using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Line;

/// <summary>Registers the block runtimes of the design and plan stations' Steps (line spec §4.1, §6.1); their descriptors stay in the block catalog.</summary>
internal static class LineBlockRuntimesServiceCollectionExtensions
{
    /// <summary>Adds each Step's runtime by its id (AddBlockRuntime), in Step id order.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLineBlockRuntimes(this IServiceCollection services) => services
        .AddEffect<StartPhaseEffect>()   // LN-WF1: starts a line's phase as a linked instance
        .AddBlockRuntime<DesignCheckStep>()
        .AddBlockRuntime<DesignEvidenceStep>()
        .AddBlockRuntime<DesignIntakeStep>()
        .AddBlockRuntime<LineDispatchStep>()
        .AddBlockRuntime<LineSeedStep>()
        .AddBlockRuntime<PlanCheckStep>()
        .AddBlockRuntime<PlanEvidenceStep>()
        .AddBlockRuntime<PlanFreezeStep>()
        .AddBlockRuntime<PlanIntakeStep>()
        .AddBlockRuntime<PlanQuestionsStep>()
        .AddBlockRuntime<PlanSettleStep>();
}
