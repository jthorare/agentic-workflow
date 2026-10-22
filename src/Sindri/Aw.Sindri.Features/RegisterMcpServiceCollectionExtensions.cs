using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.SetupWatch;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the RegisterMcp job (setup-once spec §5.4).</summary>
internal static class RegisterMcpServiceCollectionExtensions
{
    /// <summary>Adds the RegisterMcp job keyed by 'mcp-' and the hash of the repo's name, and its admission.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRegisterMcpJob(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var resolved = new ResolvedMcpRepos();
        return services
            .AddSingleton(resolved)
            .AddScoped<McpEvents>().AddScoped<IMcpJobStarter, McpJobStarter>().AddScoped<McpNeed>().AddScoped<IMcpNeed>(static sp => new ChangedMcpNeed(sp.GetRequiredService<McpNeed>(), sp.GetRequiredService<IApprovedProfile>(), sp.GetRequiredService<IEndpointFile>(), sp.GetRequiredService<McpAutoMemory>()))
            .AddScoped(static sp => new AutomaticMcpRegistration(new RegisterMcpService(sp.GetRequiredService<IApprovedProfile>(), sp.GetRequiredService<IRepoLocator>(), sp.GetRequiredService<IEndpointFile>(), sp.GetRequiredService<IClaudeCli>(), sp.GetRequiredService<IProviderHost>()) { WriteCodexTrust = false }))
            .AddScoped<IJobAdmission<RegisterMcpJobRequest>, RegisterMcpAdmission>()
            .AddJob<RegisterMcpJobRequest, RegisterMcpResponse, RegisterMcpJobService>(resolved.KeyOf, static r => "registered in " + r.Repo.Value, repo: static r => r.Repo is { } named && RepoName.TryFrom(named, out var name) ? name : null);
    }
}
