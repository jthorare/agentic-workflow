using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Scoping;

/// <summary>Registers the scoping station's three services and the block runtimes of its Steps (scoping spec §8.2, §8.4).</summary>
internal static class ScopingServiceCollectionExtensions
{
    /// <summary>Adds 'scoping check', 'scoping show' and 'scoping desk' as scoped services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddScopingSlices(this IServiceCollection services) => services
        .AddScoped<IService<ScopingCheckRequest, ScopingCheckResponse>, ScopingCheckService>()
        .AddScoped<IService<ScopingShowRequest, ScopingView>, ScopingShowService>()
        .AddScoped<IService<ScopingDeskRequest, ScopingDeskResponse>, ScopingDeskService>()
        .AddScoped<ScopingInstances>()
        .AddBlockRuntime<BriefSeedStep>()
        .AddBlockRuntime<InterviewRecordStep>()
        .AddBlockRuntime<ChallengeLogStep>()
        .AddBlockRuntime<ScopeAnchorStep>()
        .AddBlockRuntime<ScopeCheckStep>()
        .AddBlockRuntime<ScopeFreezeStep>()
        .AddBlockRuntime<ScopeGatherStep>()
        .AddBlockRuntime<ScopeIntakeStep>()
        .AddBlockRuntime<ScopeProposeStep>()
        .AddBlockRuntime<ScopeReadBriefStep>()
        .AddBlockRuntime<ScopeReanchorStep>()
        .AddBlockRuntime<ScopeWithdrawStep>();
}
