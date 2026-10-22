using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Deps;
using Aw.CodeIndex.Infrastructure.Parsing;
using Aw.CodeIndex.Infrastructure.Scrubbing;
using Aw.CodeIndex.Infrastructure.Snapshot;
using Aw.CodeIndex.Infrastructure.Store;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.CodeIndex.Infrastructure;

/// <summary>Registers Aw.CodeIndex.Infrastructure's adapters and settings. A second technology for a port gets its own AddCodeIndex&lt;Tech&gt;Infrastructure method.</summary>
public static class CodeIndexInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the CodeIndex adapters and their settings; the host registers the <see cref="TimeProvider"/> they use.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddCodeIndexInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);
        var settings = SettingsFromEnvironment.Read(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton<IRepoSnapshot, GitRepoSnapshot>()
            .AddSingleton<ISymbolParser, CSharpParser>()
            .AddSingleton<IBodyScrubber, RedactingBodyScrubber>()
            .AddSingleton<IParseQuarantine>(_ => new FileParseQuarantine(settings.IndexDirectory))
            .AddSingleton<IManifestReader, PackageJsonReader>()
            .AddSingleton<IManifestReader, CsprojReader>()
            .AddSingleton<IManifestReader, CentralPackagesReader>()
            .AddSingleton<IIndexStore>(provider => new SqliteIndexStore(settings.IndexDirectory, provider.GetRequiredService<TimeProvider>()));
    }
}
