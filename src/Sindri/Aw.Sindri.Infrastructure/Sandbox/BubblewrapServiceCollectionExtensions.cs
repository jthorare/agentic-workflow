using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// Registers the Linux sandbox and its owner-copy and separate-login sources (sandbox spec SB12, SB18). The product Host reaches it through 'AddSindriPlatformSandbox' (the SB-5a switch), on Linux only.
/// </summary>
public static class BubblewrapServiceCollectionExtensions
{
    /// <summary>Adds the bubblewrap sandbox and the owner-copy and separate-login sources.</summary>
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
                new OwnerCopyOptions(home, sp.GetRequiredService<SindriSettings>().Home), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IModelCredentialSource>(sp => new SeparateLoginCredentialSource(sp.GetRequiredService<SindriSettings>().Home, sp.GetRequiredService<TimeProvider>()));
    }
}
