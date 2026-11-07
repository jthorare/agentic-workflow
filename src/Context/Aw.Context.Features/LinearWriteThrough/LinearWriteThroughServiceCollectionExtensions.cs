using Aw.Context.Core.Facade;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Features.LinearWriteThrough;

/// <summary>Registers the write-through slice and its port, part of AddContextFeatures.</summary>
internal static class LinearWriteThroughServiceCollectionExtensions
{
    /// <summary>The slice is scoped and the port's caller (the Linear writes) a singleton, so the port runs the slice in a scope of its own per store.</summary>
    /// <param name="services">The container.</param>
    /// <returns>The container.</returns>
    public static IServiceCollection AddLinearWriteThrough(this IServiceCollection services) => services
        .AddScoped<IService<LinearWriteThroughRequest, LinearWriteThroughResponse>, LinearWriteThroughService>()
        .AddSingleton<ILinearWriteThrough>(static sp => new ScopedLinearWriteThrough(async (request, cancellationToken) =>
        {
            var scope = sp.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                return await scope.ServiceProvider.GetRequiredService<IService<LinearWriteThroughRequest, LinearWriteThroughResponse>>().Handle(request, cancellationToken).ConfigureAwait(false);
            }
        }));
}
