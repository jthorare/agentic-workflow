using Aw.Evidence.Features.ApiRun;
using Aw.Evidence.Features.CheckProvenance;
using Aw.Evidence.Features.DbRun;
using Aw.Evidence.Features.DesktopRun;
using Aw.Evidence.Features.Doctor;
using Aw.Evidence.Features.GhPublish;
using Aw.Evidence.Features.InstallBrowser;
using Aw.Evidence.Features.Parity;
using Aw.Evidence.Features.ParityComment;
using Aw.Evidence.Features.ParityPlan;
using Aw.Evidence.Features.ParityPut;
using Aw.Evidence.Features.ParityStale;
using Aw.Evidence.Features.PublishPlan;
using Aw.Evidence.Features.RunScript;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Features;

/// <summary>Registers Aw.Evidence.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class EvidenceFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Evidence use case as an IService, with the helpers the browser slices use (Core's step runner and visual gate come from AddEvidenceCore).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvidenceFeatures(this IServiceCollection services) =>
        services.AddBrowserSlices().AddPublishSlices().AddStackSlices().AddApiSlices()
            .AddDbSlices().AddDesktopSlices();

    private static IServiceCollection AddBrowserSlices(this IServiceCollection services) =>
        services
            .AddScoped<ScriptFiles>()
            .AddScoped<ScriptRun>()
            .AddScoped<RunVerdict>()
            .AddScoped<ParityFilesAccess>()
            .AddScoped<ParityRun>()
            .AddScoped<FrameCapture>()
            .AddScoped<FrameComparison>()
            .AddScoped<RunScriptService>()
            .AddScoped<IValidator<RunScriptRequest>, RunScriptValidator>()
            .AddScoped<IService<RunScriptRequest, RunScriptResponse>>(static sp =>
                new ValidatingService<RunScriptRequest, RunScriptResponse>(
                    sp.GetRequiredService<RunScriptService>(),
                    sp.GetRequiredService<IValidator<RunScriptRequest>>()))
            .AddScoped<ParityService>()
            .AddScoped<IValidator<ParityRequest>, ParityValidator>()
            .AddScoped<IService<ParityRequest, ParityResponse>>(static sp =>
                new ValidatingService<ParityRequest, ParityResponse>(
                    sp.GetRequiredService<ParityService>(),
                    sp.GetRequiredService<IValidator<ParityRequest>>()));

    private static IServiceCollection AddPublishSlices(this IServiceCollection services) =>
        services
            .AddScoped<ParityPlanService>()
            .AddScoped<IValidator<ParityPlanRequest>, ParityPlanValidator>()
            .AddScoped<IService<ParityPlanRequest, ParityPlanResponse>>(static sp =>
                new ValidatingService<ParityPlanRequest, ParityPlanResponse>(
                    sp.GetRequiredService<ParityPlanService>(),
                    sp.GetRequiredService<IValidator<ParityPlanRequest>>()))
            .AddScoped<ParityCommentService>()
            .AddScoped<IValidator<ParityCommentRequest>, ParityCommentValidator>()
            .AddScoped<IService<ParityCommentRequest, ParityCommentResponse>>(static sp =>
                new ValidatingService<ParityCommentRequest, ParityCommentResponse>(
                    sp.GetRequiredService<ParityCommentService>(),
                    sp.GetRequiredService<IValidator<ParityCommentRequest>>()))
            .AddScoped<ParityStaleService>()
            .AddScoped<IValidator<ParityStaleRequest>, ParityStaleValidator>()
            .AddScoped<IService<ParityStaleRequest, ParityStaleResponse>>(static sp =>
                new ValidatingService<ParityStaleRequest, ParityStaleResponse>(
                    sp.GetRequiredService<ParityStaleService>(),
                    sp.GetRequiredService<IValidator<ParityStaleRequest>>()))
            .AddScoped<ParityPutService>()
            .AddScoped<IValidator<ParityPutRequest>, ParityPutValidator>()
            .AddScoped<IService<ParityPutRequest, ParityPutResponse>>(static sp =>
                new ValidatingService<ParityPutRequest, ParityPutResponse>(
                    sp.GetRequiredService<ParityPutService>(),
                    sp.GetRequiredService<IValidator<ParityPutRequest>>()))
            .AddScoped<PublishPlanService>()
            .AddScoped<IValidator<PublishPlanRequest>, PublishPlanValidator>()
            .AddScoped<IService<PublishPlanRequest, PublishPlanResponse>>(static sp =>
                new ValidatingService<PublishPlanRequest, PublishPlanResponse>(
                    sp.GetRequiredService<PublishPlanService>(),
                    sp.GetRequiredService<IValidator<PublishPlanRequest>>()))
            .AddScoped<GhPublishService>()
            .AddScoped<IValidator<GhPublishRequest>, GhPublishValidator>()
            .AddScoped<IService<GhPublishRequest, GhPublishResponse>>(static sp =>
                new ValidatingService<GhPublishRequest, GhPublishResponse>(
                    sp.GetRequiredService<GhPublishService>(),
                    sp.GetRequiredService<IValidator<GhPublishRequest>>()));

    private static IServiceCollection AddStackSlices(this IServiceCollection services) =>
        services
            .AddScoped<DoctorService>()
            .AddScoped<IValidator<DoctorRequest>, DoctorValidator>()
            .AddScoped<IService<DoctorRequest, DoctorResponse>>(static sp =>
                new ValidatingService<DoctorRequest, DoctorResponse>(
                    sp.GetRequiredService<DoctorService>(),
                    sp.GetRequiredService<IValidator<DoctorRequest>>()))
            .AddScoped<CheckProvenanceService>()
            .AddScoped<IValidator<CheckProvenanceRequest>, CheckProvenanceValidator>()
            .AddScoped<IService<CheckProvenanceRequest, CheckProvenanceResponse>>(static sp =>
                new ValidatingService<CheckProvenanceRequest, CheckProvenanceResponse>(
                    sp.GetRequiredService<CheckProvenanceService>(),
                    sp.GetRequiredService<IValidator<CheckProvenanceRequest>>()))
            .AddScoped<InstallBrowserService>()
            .AddScoped<IValidator<InstallBrowserRequest>, InstallBrowserValidator>()
            .AddScoped<IService<InstallBrowserRequest, InstallBrowserResponse>>(static sp =>
                new ValidatingService<InstallBrowserRequest, InstallBrowserResponse>(
                    sp.GetRequiredService<InstallBrowserService>(),
                    sp.GetRequiredService<IValidator<InstallBrowserRequest>>()));

    private static IServiceCollection AddApiSlices(this IServiceCollection services) =>
        services
            .AddScoped<ApiVetting>()
            .AddScoped<RequestRunner>()
            .AddScoped<CallRunner>()
            .AddScoped<ApiRunService>()
            .AddScoped<IValidator<ApiRunRequest>, ApiRunValidator>()
            .AddScoped<IService<ApiRunRequest, ApiRunResponse>>(static sp =>
                new ValidatingService<ApiRunRequest, ApiRunResponse>(
                    sp.GetRequiredService<ApiRunService>(),
                    sp.GetRequiredService<IValidator<ApiRunRequest>>()));

    private static IServiceCollection AddDbSlices(this IServiceCollection services) =>
        services
            .AddScoped<DbVetting>()
            .AddScoped<DbQueryRunner>()
            .AddScoped<DbRunService>()
            .AddScoped<IValidator<DbRunRequest>, DbRunValidator>()
            .AddScoped<IService<DbRunRequest, DbRunResponse>>(static sp =>
                new ValidatingService<DbRunRequest, DbRunResponse>(
                    sp.GetRequiredService<DbRunService>(),
                    sp.GetRequiredService<IValidator<DbRunRequest>>()));

    private static IServiceCollection AddDesktopSlices(this IServiceCollection services) =>
        services
            .AddScoped<DesktopPlans>()
            .AddScoped<DesktopDriving>()
            .AddScoped<DesktopStack>()
            .AddScoped<DesktopFrames>()
            .AddScoped<DesktopRunService>()
            .AddScoped<IValidator<DesktopRunRequest>, DesktopRunValidator>()
            .AddScoped<IService<DesktopRunRequest, DesktopRunResponse>>(static sp =>
                new ValidatingService<DesktopRunRequest, DesktopRunResponse>(
                    sp.GetRequiredService<DesktopRunService>(),
                    sp.GetRequiredService<IValidator<DesktopRunRequest>>()));
}
