using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.PingProcess;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers Aw.Sindri.Features's slices: the command layer every client (CLI, API, UI) calls (factory addendum §17.27.1).</summary>
public static class SindriFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Sindri use case as an IService, the background services as IScheduledService, and the workflow blocks this build ships.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IService<GetStatusRequest, GetStatusResponse>, GetStatusService>()
            .AddScoped<IService<StartProcessRequest, StartProcessResponse>, StartProcessService>()
            .AddScoped<IService<StopProcessRequest, StopProcessResponse>, StopProcessService>()
            .AddScoped<IService<RecordHeartbeatRequest, RecordHeartbeatResponse>, RecordHeartbeatService>()
            .AddScoped<IService<StreamEventsRequest, StreamEventsResponse>, StreamEventsService>()
            .AddScoped<IService<PingProcessRequest, Ping>, PingProcessService>()
            .AddScoped<IService<ValidateProfileRequest, ValidateProfileResponse>, ValidateProfileService>()
            .AddScoped<IService<ApproveProfileRequest, ApproveProfileResponse>, ApproveProfileService>()
            .AddScoped<ProfileFacts>()
            .AddScoped<IService<RunDoctorRequest, RunDoctorResponse>, RunDoctorService>()
            .AddScoped<IScheduledService, HeartbeatService>()
            .AddScoped<IService<ValidateRecipeRequest, ValidateRecipeResponse>, ValidateRecipeService>()
            .AddScoped<IService<EvalRecipeRequest, EvalRecipeResponse>, EvalRecipeService>()
            .AddRecipeBlocks();
        return services;
    }
}
