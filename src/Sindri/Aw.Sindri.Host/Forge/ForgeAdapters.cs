using Aw.Forge.Core.Credentials;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure;
using Aw.Kernel;
using Aw.Sindri.Core;
using Results = Aw.Kernel.Results;
using SindriForgeSettings = Aw.Sindri.Core.Profile.ForgeSettings;

namespace Aw.Sindri.Host.Forge;

/// <summary>
/// Builds the forge adapter a profile's 'workflow.forge' block names, in a container of its own: the forge package's infrastructure registers its own settings
/// and credential sources, none of which may join Sindri's container. The choices are those of aw-forge-fake's composition root: the file adapter by default,
/// hosts defaulting to forge.test (file) or github.com (github), the GitHub adapter bound to exactly one host, and the credential env:AW_FORGE_TOKEN unless named.
/// </summary>
internal static class ForgeAdapters
{
    /// <summary>Builds the adapter.</summary>
    /// <param name="settings">The approved profile's 'workflow.forge' block, with an adapter.</param>
    /// <param name="environment">Reads an environment variable (AW_STATE_DIR for the file adapter's state, the credential's variable for GitHub).</param>
    /// <param name="clock">The clock the command credential source caches by.</param>
    /// <returns>The adapter, or SND-FORGE-002 when the block can't be used (the profile check refuses most of these first).</returns>
    internal static Result<IForge> Create(SindriForgeSettings settings, Func<string, string?> environment, TimeProvider clock)
    {
        var github = string.Equals(settings.Adapter, "github", StringComparison.Ordinal);
        var hosts = new ForgeHosts(settings.Hosts is { Count: > 0 } named ? named : [github ? "github.com" : "forge.test"]);
        var services = new ServiceCollection().AddSingleton(clock);
        if (!github)
        {
            return Results.Success(services.AddForgeFileInfrastructure(environment, hosts).BuildServiceProvider().GetRequiredService<IForge>());
        }

        if (hosts.Names.Count != 1)
        {
            return SindriErrors.ForgeRefused.With("workflow.forge.hosts: the github adapter takes exactly one host (its credential is bound to it)");
        }

        var credential = settings.Credential is { Length: > 0 } value ? ForgeCredentialSpec.Parse(value) : ForgeCredentialSpec.Default;
        return credential.IsError
            ? SindriErrors.ForgeRefused.With($"workflow.forge.credential: {credential.Error.Message}")
            : Results.Success(services.AddForgeGitHubInfrastructure(environment, hosts, credential.Value).BuildServiceProvider().GetRequiredService<IForge>());
    }
}
