using Aw.Redaction;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Infrastructure.Reading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Transcripts.Infrastructure;

/// <summary>Registers Aw.Transcripts.Infrastructure's adapters and settings: the file-backed <see cref="ITranscriptCorpus"/>.</summary>
public static class TranscriptsInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the file-backed corpus over the default roots (each provider's home under HOME, and Sindri's agent homes).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTranscriptsInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return services.AddTranscriptsInfrastructure(SettingsFromEnvironment.Read(environment).Roots);
    }

    /// <summary>Adds the file-backed corpus over the given roots only.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="roots">The transcript roots, one provider each.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTranscriptsInfrastructure(this IServiceCollection services, IReadOnlyList<TranscriptRoot> roots)
    {
        services.TryAddSingleton<IRedactor>(Redactor.Default);
        return services
            .AddSingleton(new TranscriptsSettings([.. roots]))
            .AddSingleton<TranscriptFileReader>()
            .AddSingleton<ITranscriptCorpus, FileTranscriptCorpus>();
    }
}
