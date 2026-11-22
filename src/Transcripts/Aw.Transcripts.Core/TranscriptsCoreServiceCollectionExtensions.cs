using Microsoft.Extensions.DependencyInjection;

namespace Aw.Transcripts.Core;

/// <summary>Registers Aw.Transcripts.Core's types.</summary>
public static class TranscriptsCoreServiceCollectionExtensions
{
    /// <summary>Adds the Transcripts domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTranscriptsCore(this IServiceCollection services) => services;
}
