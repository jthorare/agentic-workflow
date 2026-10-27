using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// Registers the Linux sandbox and its owner-copy login source (sandbox spec SB12, SB18). Nothing calls it yet: the product Host registers only 'UnavailableSandbox'
/// until the launcher integration (SB-5a) switches it over, and a test pins that.
/// </summary>
public static class BubblewrapServiceCollectionExtensions
{
    /// <summary>Adds the bubblewrap sandbox and the owner-copy source.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriBubblewrapSandbox(this IServiceCollection services, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);
        var home = environment("HOME") is { Length: > 0 } configured ? configured : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return services
            .AddSingleton<ISandboxProcesses, SandboxProcesses>()
            .AddSingleton<LaunchSecretRegistry>()
            .AddSingleton<ILaunchSecrets>(static sp => sp.GetRequiredService<LaunchSecretRegistry>())
            .AddSingleton<ISandbox>(sp => new BubblewrapSandbox(BubblewrapOptions.From(environment), sp.GetRequiredService<ISandboxProcesses>(), new HostPaths(), sp.GetRequiredService<ILaunchSecrets>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IModelCredentialSource>(sp => new OwnerCopyCredentialSource(
                new OwnerCopyOptions(home, sp.GetRequiredService<SindriSettings>().Home), sp.GetRequiredService<TimeProvider>()));
    }
}
