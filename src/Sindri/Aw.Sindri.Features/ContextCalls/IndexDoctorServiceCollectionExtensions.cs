using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Registers doctor's 'context:index-*' rows (owned index, Part of #1394).</summary>
internal static class IndexDoctorServiceCollectionExtensions
{
    /// <summary>Adds the rows and a records-embedder probe that never answers, which a host with an Ollama probe replaces.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddIndexDoctor(this IServiceCollection services) => services
        .AddSingleton<IRecordEmbedderProbe, NoRecordEmbedderProbe>()
        .AddScoped<IDoctorChecks>(static sp => new IndexDoctorChecks(
            sp.GetRequiredService<IApprovedProfile>(), sp.GetRequiredService<ContextScopes>(), sp.GetRequiredService<IContextEngine>(),
            sp.GetRequiredService<ISecretPointers>(), sp.GetRequiredService<IRecordEmbedderProbe>(), TimeSpan.FromSeconds(3)));
}
