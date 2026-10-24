using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.LineDoctor;

/// <summary>'sindri line doctor' (line spec Appendix D), in process.</summary>
internal static class LineDoctorServiceCollectionExtensions
{
    /// <summary>Registers the line doctor.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLineDoctor(this IServiceCollection services) => services
        .AddScoped<IService<LineDoctorRequest, LineDoctorResponse>, LineDoctorService>();
}
