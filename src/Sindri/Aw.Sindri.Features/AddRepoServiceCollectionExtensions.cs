using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.AddRepo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features;

/// <summary>Registers AddRepo (setup-once spec §5.1): the job, its admission, and the git facts every setup read shares.</summary>
internal static class AddRepoServiceCollectionExtensions
{
    /// <summary>Adds the AddRepo job keyed by the hash of the resolved path, its admission and the git facts cache.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddAddRepo(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var resolved = new ResolvedRepos();
        services.TryAddSingleton<IShippedEffects, NoShippedEffects>();   // until the host supplies the embedded set's
        return services
            .AddSingleton(resolved)
            .AddSingleton<RepoGitCache>()
            .AddSingleton<KnownRepoEvents>()
            .AddScoped<RepoEvents>()
            .AddScoped<RepoAddedBackfill>()
            .AddScoped<IJobAdmission<AddRepoRequest>, AddRepoAdmission>()
            .AddJob<AddRepoRequest, AddedRepo, AddRepoService>(resolved.KeyOf, static r => "added " + r.Repo.Value);
    }
}
