using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Skills;

internal static class SkillBlockServiceCollectionExtensions
{
    /// <summary>Loads the skills folder once, registers the 'step.skill.*' descriptors with the block catalog so recipes can name them, and the <see cref="ISkillBlocks"/> the API reads (skill flows spec §3.2).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSkillBlocks(this IServiceCollection services, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        var registry = new SkillBlockRegistry(SkillFolders.Resolve(environment, Directory.GetCurrentDirectory()));
        foreach (var block in registry.Loaded)
        {
            _ = services.AddBlockDescriptor(block.Descriptor);
        }

        return services.AddSingleton<ISkillBlocks>(registry);
    }
}
