using System.Text.Json;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Profile;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Registration;

/// <summary>
/// What a CLI verb that only calls the running control plane needs (#3727): the settings, the profile, the control plane's
/// endpoint file, probe and launcher, the human token and the repo locator. AddSindriInfrastructure registers the same
/// adapters through the same method, so the two compositions can't drift apart.
/// </summary>
public static class SindriClientServiceCollectionExtensions
{
    /// <summary>Adds the settings and the client adapters, and nothing that opens a store, reads the skills folder or starts an engine.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriClientInfrastructure(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton(SettingsFromEnvironment.Read(environment)).AddClientAdapters(environment);

    /// <summary>The client adapters, without the settings (the caller registers them once).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddClientAdapters(this IServiceCollection services, Func<string, string?> environment) => services
        .AddSingleton<IProfileSource>(sp => new YamlProfileSource(sp.GetRequiredService<SindriSettings>(), sp.GetRequiredService<JsonSerializerOptions>(), () => HeavyCap.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory)))
        .AddSingleton(static sp => new LiveApprovedProfile(sp.GetRequiredService<IProfileSource>(), sp.GetRequiredService<SindriSettings>().ProfilePath)).AddSingleton<IApprovedProfile>(static sp => sp.GetRequiredService<LiveApprovedProfile>()).AddSingleton<IProfileCache>(static sp => sp.GetRequiredService<LiveApprovedProfile>())
        .AddSingleton<IEndpointFile, FileEndpoint>().AddSingleton<IControlPlaneProbe>(static sp => new HttpControlPlaneProbe(sp.GetRequiredService<IEndpointFile>(), sp.GetRequiredService<JsonSerializerOptions>(), handler: ControlPlaneHandler.Shared))   // the CLI's ping and its request share one handler (#3727)
        .AddSingleton<IControlPlaneLauncher, ServeLauncher>()
        .AddSingleton<IHumanTokenStore, FileHumanToken>()
        .AddSingleton<IRepoLocator, GitRepoLocator>();
}
