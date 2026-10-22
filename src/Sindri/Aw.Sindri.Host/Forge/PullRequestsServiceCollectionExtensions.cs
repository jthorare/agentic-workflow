using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Forge;

/// <summary>Registers Sindri's pull-request port.</summary>
internal static class PullRequestsServiceCollectionExtensions
{
    /// <summary>Adds <see cref="IPullRequests"/>: the forge the approved profile names, or the no-forge answer when it names none.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPullRequests(this IServiceCollection services, Func<string, string?> environment) => services
        .AddSingleton<IPullRequests>(sp => new ConfiguredPullRequests(sp.GetRequiredService<IApprovedProfile>(), environment, sp.GetRequiredService<TimeProvider>()));
}
