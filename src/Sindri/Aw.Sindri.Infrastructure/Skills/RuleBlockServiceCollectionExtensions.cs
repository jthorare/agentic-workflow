using Aw.Sindri.Core.Graph.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Skills;

internal static class RuleBlockServiceCollectionExtensions
{
    /// <summary>Registers the rule catalog the 'rule_catalog' block reads (issue 3787).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRuleBlocks(this IServiceCollection services, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IRuleCatalogPort>(new FileRuleCatalog(RuleFolders.Resolve(environment, Directory.GetCurrentDirectory())));
    }
}
