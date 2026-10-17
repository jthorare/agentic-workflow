using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Launching;

internal static class LaunchingServiceCollectionExtensions
{
    internal static IServiceCollection AddLaunchSlices(this IServiceCollection services) => services
        .AddSingleton<LaunchWork>()
        .AddSingleton(static sp => LaunchTimings.For(sp.GetRequiredService<TimeProvider>()))
        .AddScoped<LaunchOutcomes>()
        .AddScoped<LaunchIntake>()
        .AddScoped<LaunchRecovery>()
        .AddScoped<LaunchPlans>()
        .AddScoped<TakeoverWait>()
        .AddScoped<AttachFlow>()
        .AddScoped<IService<RunLaunchRequest, RunLaunchResponse>, RunLaunch>()
        .AddScoped<IService<ProcessLaunchRequest, ProcessLaunchResponse>, ProcessLaunch>();
}
