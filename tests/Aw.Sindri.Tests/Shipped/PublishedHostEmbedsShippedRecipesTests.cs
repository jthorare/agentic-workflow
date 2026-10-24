using Aw.Sindri.Host.Shipped;

namespace Aw.Sindri.Tests.Shipped;

/// <summary>The Host assembly as an ordinary publish builds it (no <c language="text">AwRecipesDir</c> given) embeds every recipe of recipes/sindri, so approve-all has a plan on a published binary.</summary>
public sealed class PublishedHostEmbedsShippedRecipesTests
{
    [Fact]
    public void The_host_assembly_embeds_every_shipped_recipe_without_AwRecipesDir()
    {
        var expected = Directory.GetFiles(Path.Combine(RepoRoot(), "recipes", "sindri"), "workflow.*.yaml").Select(static p => Path.GetFileName(p)).Order(StringComparer.Ordinal).ToList();

        var embedded = new ManifestEmbeddedRecipes(typeof(ManifestEmbeddedRecipes).Assembly).Files()
            .Select(static f => f.RelativePath).Where(static p => !p.Contains('/', StringComparison.Ordinal) && p.EndsWith(".yaml", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, embedded);
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
