using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Skills;

/// <summary>Binds <see cref="FileSkillCatalog"/> to <see cref="SkillCatalogPortContract"/> over a temp skills folder: one '&lt;name&gt;/SKILL.md' per seed.</summary>
[Lane(TestLane.Integration)]
public sealed class FileSkillCatalogContractTests : SkillCatalogPortContract, IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("aw-skill-catalog-");

    public void Dispose() => _folder.Delete(recursive: true);

    protected override async ValueTask<ISkillCatalogPort> Create(IReadOnlyList<SkillCatalogSeed> skills)
    {
        foreach (var skill in skills)
        {
            var folder = Directory.CreateDirectory(Path.Combine(_folder.FullName, skill.Name));
            var description = skill.Description is { } text ? $"description: {text}\n" : string.Empty;
            await File.WriteAllTextAsync(Path.Combine(folder.FullName, "SKILL.md"), $"---\nname: {skill.Name}\n{description}---\n\n# {skill.Name}\n", TestContext.Current.CancellationToken);
        }

        return new FileSkillCatalog(_folder.FullName);
    }
}
