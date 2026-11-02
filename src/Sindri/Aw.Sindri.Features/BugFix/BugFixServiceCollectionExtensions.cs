using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.BugFix;

/// <summary>Registers the block runtimes of the bugfix blocks' Steps (DT spec 6.1); their descriptors are registered with the catalog (BlockRegistrations).</summary>
internal static class BugFixServiceCollectionExtensions
{
    /// <summary>Adds the four bugfix Steps' runtimes by block id and what the bug-fix agent Steps are told (#1682).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddBugFixSlices(this IServiceCollection services) => services
        .AddSingleton<IAgentStepInstructions, InvestigateInstructions>()
        .AddSingleton<IAgentStepInstructions, ReproduceInstructions>()
        .AddSingleton<IAgentStepInstructions, ImplementFixInstructions>()
        .AddSingleton<IAgentStepInstructions, ReportInstructions>()
        .AddBlockRuntime<StartAttemptStep>()
        .AddBlockRuntime<RecordCandidateStep>()
        .AddBlockRuntime<RecheckStep>()
        .AddBlockRuntime<JudgeStep>();
}
