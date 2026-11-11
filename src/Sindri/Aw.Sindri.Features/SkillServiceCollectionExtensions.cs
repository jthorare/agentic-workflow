using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetSkills;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

internal static class SkillServiceCollectionExtensions
{
    /// <summary>Adds GetSkills, the query behind GET /sindri/skills and 'sindri skills' (skill flows spec §7).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddGetSkills(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<GetSkillsRequest>, AcceptAllValidator<GetSkillsRequest>>().AddQuery<GetSkillsRequest, GetSkillsResponse, GetSkillsService>();
    }
}
