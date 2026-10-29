using Aw.Kernel.Services;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Features.AllowDenial;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.EndLearnMode;
using Aw.Sindri.Features.GetSandboxStatus;
using Aw.Sindri.Features.ListDenials;
using Aw.Sindri.Features.PlanSandbox;
using Aw.Sindri.Features.Prepare;
using Aw.Sindri.Features.RecordLimitHit;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the sandbox slices (sandbox spec §12, SB-1 onward).</summary>
internal static class SandboxServiceCollectionExtensions
{
    /// <summary>Adds PlanSandbox, the plan builder behind 'sindri sandbox show' and 'explain'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPlanSandbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IService<PlanSandboxRequest, PlanSandboxResponse>, PlanSandboxService>();
    }

    /// <summary>Adds RecordLimitHit, the issue 'session wrap' makes when a limit is reached that does not end the session (proven by the launch secret).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecordLimitHit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<RecordLimitHitRequest>, AcceptAllValidator<RecordLimitHitRequest>>().AddIssue<RecordLimitHitRequest, RecordLimitHitResponse, RecordLimitHitService>();
    }

    /// <summary>Adds EndLearnMode, the desk command behind 'sindri sandbox learn off'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddEndLearnMode(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<EndLearnModeRequest>, AcceptAllValidator<EndLearnModeRequest>>().AddCommand<EndLearnModeRequest, CommandAccepted, EndLearnModeService>();
    }

    /// <summary>Adds AllowDenial, the desk command behind the Allow button on a blocked host or home path.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddAllowDenial(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<AllowDenialRequest>, AcceptAllValidator<AllowDenialRequest>>().AddCommand<AllowDenialRequest, AllowDenialResponse, AllowDenialService>();
    }

    /// <summary>Adds ListDenials, the query behind 'sindri sandbox denials'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddListDenials(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<ListDenialsRequest>, AcceptAllValidator<ListDenialsRequest>>().AddQuery<ListDenialsRequest, ListDenialsResponse, ListDenialsService>();
    }

    /// <summary>Adds CheckSandbox, the query behind 'sindri sandbox check' and Check again, with the checks it runs and doctor's sandbox rows (SB-5a).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddCheckSandbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<CheckSandboxRequest>, AcceptAllValidator<CheckSandboxRequest>>().AddQuery<CheckSandboxRequest, CheckSandboxResponse, CheckSandboxService>()
            .AddScoped<IValidator<GetSandboxStatusRequest>, AcceptAllValidator<GetSandboxStatusRequest>>()
            .AddQuery<GetSandboxStatusRequest, SandboxStatusResponse, GetSandboxStatusService>().AddSingleton<ISandboxPlatformName, OperatingSystemPlatformName>()
            .AddSingleton<LastSandboxCheck>().AddScoped<SandboxChecks>().AddScoped<SandboxResumeList>()
            .AddScoped<ISandboxChecks>(static sp => new RecordingSandboxChecks(sp.GetRequiredService<SandboxChecks>(), sp.GetRequiredService<LastSandboxCheck>(), sp.GetRequiredService<TimeProvider>()))
            .AddScoped<ISandboxFormCheck, SandboxFormCheck>().AddScoped<ISandboxLoginCheck, SandboxLoginCheck>().AddScoped<ISandboxFixtureLogin, SandboxFixtureLogin>()
            .AddScoped<ISandboxParks, SandboxParkedInstances>().AddScoped<ISandboxSlots, SandboxSlots>()
            .AddSingleton<ISandboxFixture, NoSandboxFixture>().AddSingleton<ISandboxMachine, ProcessSandboxMachine>()
            .AddScoped<IDoctorChecks, SandboxDoctorChecks>();
    }

    /// <summary>Adds the host-side prepare of a Step's worktree (SB-11), which the launcher calls after its guard and before the sandbox.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddWorkspacePrepare(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IWorkspacePrepare>(static sp => new WorkspacePrepare(
            sp.GetRequiredService<IApprovedProfile>(), sp.GetRequiredService<IPrepareSource>(), sp.GetRequiredService<IPrepareCache>(),
            sp.GetRequiredService<ICheckCommands>(), sp.GetRequiredService<IHeavySlots>(), sp.GetRequiredService<TimeProvider>()));
    }
}
