using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceInvestigate;
using Aw.BugFix.Features.AdvanceReport;
using Aw.BugFix.Features.AdvanceReproduce;
using Aw.BugFix.Features.Init;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Features.RecordCandidate;
using Aw.BugFix.Features.RecordRun;
using Aw.BugFix.Features.Resume;
using Aw.BugFix.Features.RunTest;
using Aw.BugFix.Features.RunUi;
using Aw.BugFix.Features.StartAttempt;
using Aw.BugFix.Features.Status;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Features;

/// <summary>Registers Aw.BugFix.Features's slices: one IService per bugfix-state command.</summary>
public static class BugFixFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every BugFix use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBugFixFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IService<InitRequest, InitResponse>, InitService>()
            .AddScoped<IService<AdvanceInvestigateRequest, AdvanceInvestigateResponse>, AdvanceInvestigateService>()
            .AddScoped<IService<AdvanceReproduceRequest, AdvanceReproduceResponse>, AdvanceReproduceService>()
            .AddScoped<IService<StartAttemptRequest, StartAttemptResponse>, StartAttemptService>()
            .AddScoped<IService<RecordCandidateRequest, RecordCandidateResponse>, RecordCandidateService>()
            .AddScoped<IService<RecordRunRequest, RecordRunResponse>, RecordRunService>()
            .AddScoped<IService<RecordBuildFailedRequest, RecordRunResponse>, RecordBuildFailedService>()
            .AddScoped<IService<JudgeCandidateRequest, JudgeCandidateResponse>, JudgeCandidateService>()
            .AddScoped<IService<AdvanceReportRequest, AdvanceReportResponse>, AdvanceReportService>()
            .AddScoped<IService<RunTestRequest, RunTestResponse>, RunTestService>()
            .AddScoped<IService<RunUiRequest, RunUiResponse>, RunUiService>()
            .AddScoped<IService<ResumeRequest, ResumeResponse>, ResumeService>()
            .AddScoped<IService<StatusRequest, BugFixState>, StatusService>();
        return services;
    }
}
