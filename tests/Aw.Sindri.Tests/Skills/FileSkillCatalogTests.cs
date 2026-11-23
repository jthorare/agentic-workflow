using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Skills;

namespace Aw.Sindri.Tests.Skills;

/// <summary>What <see cref="FileSkillCatalog"/> reads from a real SKILL.md beyond the contract: folded and quoted descriptions, the shared folders and a missing skills folder.</summary>
[Lane(TestLane.Integration)]
public sealed class FileSkillCatalogTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("aw-skill-catalog-");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public async Task A_folded_description_is_joined_and_a_quoted_one_loses_its_quotes()
    {
        Write("folded", "---\nname: folded\ndescription: >\n  Use when a plan needs a check:\n  product, design and security.\nlicense: MIT\n---\n# folded\n");
        Write("quoted", "---\nname: quoted\ndescription: \"Report-only variant: no fixes.\"\n---\n");

        var listed = (await new FileSkillCatalog(_folder.FullName).List(Token)).Value;

        Assert.Equal(
            [new SkillListing("folded", "Use when a plan needs a check: product, design and security."), new SkillListing("quoted", "Report-only variant: no fixes.")],
            listed);
    }

    [Fact]
    public async Task Shared_folders_and_folders_without_a_SKILL_md_are_not_skills_and_a_file_without_front_matter_has_an_empty_description()
    {
        Write("_shared", "---\ndescription: shared\n---\n");
        Directory.CreateDirectory(Path.Combine(_folder.FullName, "notes"));
        Write("plain", "# plain\nNo front matter here.\n");

        var listed = (await new FileSkillCatalog(_folder.FullName).List(Token)).Value;

        Assert.Equal([new SkillListing("plain", string.Empty)], listed);
    }

    [Fact]
    public async Task A_missing_skills_folder_lists_no_skill()
    {
        var listed = await new FileSkillCatalog(Path.Combine(_folder.FullName, "absent")).List(Token);

        Assert.True(listed.IsSuccess);
        Assert.Empty(listed.Value);
    }

    private void Write(string skill, string text)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_folder.FullName, skill));
        File.WriteAllText(Path.Combine(folder.FullName, "SKILL.md"), text);
    }
}
