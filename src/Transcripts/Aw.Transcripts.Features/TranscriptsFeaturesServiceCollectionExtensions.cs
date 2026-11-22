using Aw.Kernel.Services;
using Aw.Transcripts.Features.ListSessions;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Transcripts.Features;

/// <summary>Registers Aw.Transcripts.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class TranscriptsFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Transcripts use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTranscriptsFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<ListSessionsService>()
            .AddScoped<IValidator<ListSessionsRequest>, ListSessionsValidator>()
            .AddScoped<IService<ListSessionsRequest, ListSessionsResponse>>(static sp =>
                new ValidatingService<ListSessionsRequest, ListSessionsResponse>(
                    sp.GetRequiredService<ListSessionsService>(),
                    sp.GetRequiredService<IValidator<ListSessionsRequest>>()));
        return services;
    }
}
