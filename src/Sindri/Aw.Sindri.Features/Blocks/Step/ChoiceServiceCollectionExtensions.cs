using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>Registers the block runtime of the decision-tree Steps (DT spec 8.2); their descriptors are registered with the catalog (BlockRegistrations).</summary>
internal static class ChoiceServiceCollectionExtensions
{
    /// <summary>Adds the runtime of 'step.judge.choose'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddChoiceSlices(this IServiceCollection services) => services.AddBlockRuntime<JudgeChooseStep>();
}
