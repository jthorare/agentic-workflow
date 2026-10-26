using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Scoping;

/// <summary>Registers the gap search's runtimes and the searcher's instructions (issue 966). The descriptors are registered with the catalog, in <c language="text">BlockRegistrations</c>.</summary>
internal static class GapSearchBlocks
{
    /// <summary>Adds the block runtimes of 'step.scope.gap-judge' and 'step.scope.gap-log' and the instructions of 'step.scope.gap-search'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddScopeGapSearch(this IServiceCollection services) => services
        .AddBlockRuntime<GapJudgeStep>()
        .AddBlockRuntime<GapLogStep>()
        .AddSingleton<IAgentStepInstructions, GapSearchInstructions>();
}
