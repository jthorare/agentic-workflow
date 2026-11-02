using Aw.Context.Core.Vectors;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Features.EmbedRecords;

internal static class EmbedRecordsRegistration
{
    /// <summary>Registers the embed job; it needs an IRecordStore and an IChunkEmbedder from the Host.</summary>
    /// <param name="services">The collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEmbedRecords(this IServiceCollection services) =>
        services.AddScoped<IService<EmbedRecordsRequest, EmbedRecordsReport>, EmbedRecordsService>();
}
