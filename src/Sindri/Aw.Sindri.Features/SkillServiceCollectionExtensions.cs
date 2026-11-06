using Aw.Kernel.Services;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetSkills;
using Aw.Sindri.Core.Workflow.RegistryWrites;
using Aw.Sindri.Features.Skills;
using Aw.Sindri.Features.StartSkillRun;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features;

internal static class SkillServiceCollectionExtensions
{
    /// <summary>Adds GetSkills, the query behind GET /sindri/skills and 'sindri skills', and the loader of skill manifests it reads through (skill flows spec §3.2, §7).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddGetSkills(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddSingleton<Func<string, SkillBlocks>>(static _ => SkillManifestLoader.Load)
            .AddSingleton<ISkillCatalog, SkillCatalog>()   // the folder is the Host's: SkillsFolder.Resolve over the environment
            .AddScoped<IValidator<GetSkillsRequest>, AcceptAllValidator<GetSkillsRequest>>().AddQuery<GetSkillsRequest, GetSkillsResponse, GetSkillsService>();
    }

    /// <summary>Adds StartSkillRun, the command behind POST /sindri/skills/{id}/runs and 'sindri skill run' (skill flows spec §7).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddStartSkillRun(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<RecipeRegistrar>();   // also registered by the shipped-recipe registration
        services.TryAddScoped<RecipeApprovalRecorder>();
        return services.AddScoped<TrialRecipes>().AddScoped<IValidator<StartSkillRunRequest>, AcceptAllValidator<StartSkillRunRequest>>().AddCommand<StartSkillRunRequest, StartSkillRunResponse, StartSkillRunService>();
    }
}
