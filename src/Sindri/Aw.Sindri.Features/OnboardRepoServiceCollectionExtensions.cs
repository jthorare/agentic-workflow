using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.OnboardRepo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features;

/// <summary>Registers onboarding after approval (setup-once spec §5.1): the hook slot, the shared onboarding the setup service runs, and the OnboardRepo job.</summary>
internal static class OnboardRepoServiceCollectionExtensions
{
    /// <summary>Adds the OnboardRepo job keyed by 'onboard-' and the hash of the repo's name, its admission, and the onboarding the setup service ticks.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddOnboardRepo(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var resolved = new ResolvedOnboardRepos();
        services.TryAddSingleton<IRepoHooks, NoSecretCheckHook>();   // until an installer ships
        return services
            .AddSingleton(resolved)
            .AddScoped<RepoOnboarding>()
            .AddScoped<IRepoOnboardingTick>(static sp => sp.GetRequiredService<RepoOnboarding>())
            .AddScoped<IJobAdmission<OnboardRepoRequest>, OnboardRepoAdmission>()
            .AddJob<OnboardRepoRequest, OnboardedRepo, OnboardRepoService>(resolved.KeyOf, static r => "onboarded " + r.Repo.Value);
    }
}
