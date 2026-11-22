using Microsoft.Extensions.DependencyInjection;

namespace Aw.Transcripts.Infrastructure;

/// <summary>Registers Aw.Transcripts.Infrastructure's adapters and settings. A second technology for a port gets its own AddTranscripts&lt;Tech&gt;Infrastructure method.</summary>
public static class TranscriptsInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Transcripts adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTranscriptsInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
