using Aw.Kernel.Services;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetSkills;
using Aw.Sindri.Features.Skills;
using Microsoft.Extensions.DependencyInjection;

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
}
