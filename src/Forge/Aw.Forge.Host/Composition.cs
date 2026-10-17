using Aw.Forge.Core;
using Aw.Forge.Core.Credentials;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host;

/// <summary>
/// The one composition root, shared by Program.cs and the DI validation test. The adapter comes from AW_FORGE_ADAPTER
/// (file, the default, or github), the allowed hosts from AW_FORGE_HOSTS (comma separated; forge.test for file, github.com
/// for github) and the GitHub credential from AW_FORGE_CREDENTIAL (env:NAME or command:argv; default env:AW_FORGE_TOKEN).
/// </summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var github = string.Equals(environment("AW_FORGE_ADAPTER"), "github", StringComparison.Ordinal);
        if (environment("AW_FORGE_ADAPTER") is { Length: > 0 } adapter && !github && adapter != "file")
        {
            throw new InvalidOperationException($"AW_FORGE_ADAPTER: '{adapter}' is not file or github");
        }

        var hosts = new ForgeHosts((environment("AW_FORGE_HOSTS") ?? (github ? "github.com" : "forge.test")).Split(','));
        if (github && hosts.Names.Count != 1)
        {
            throw new InvalidOperationException("AW_FORGE_HOSTS: the github adapter takes exactly one host (its credential is bound to it)");
        }

        _ = services
            .AddSingleton(clock)
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("aw-forge-fake", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddForgeCore();
        return github ? AddGitHub(services, environment, hosts) : services.AddForgeFileInfrastructure(environment, hosts);
    }

    private static IServiceCollection AddGitHub(IServiceCollection services, Func<string, string?> environment, ForgeHosts hosts)
    {
        var credential = environment("AW_FORGE_CREDENTIAL") is { Length: > 0 } value ? ForgeCredentialSpec.Parse(value) : ForgeCredentialSpec.Default;
        return credential.IsError
            ? throw new InvalidOperationException($"AW_FORGE_CREDENTIAL: {credential.Error.Message}")
            : services.AddForgeGitHubInfrastructure(environment, hosts, credential.Value);
    }
}
