using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>What the owner may type for a Linear project: a key, or a project url whose slug ends in the slug id.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearProjectRefsTests
{
    [Fact]
    public void A_key_is_kept_as_typed()
    {
        var parsed = LinearProjectRefs.Parse(ProjectRows.LinearKey);

        Expect.Equal((ProjectRows.LinearKey, null), (parsed.Value.Key, parsed.Value.Slug));
    }

    [Theory]
    [InlineData("https://linear.app/acme/project/checkout-rewrite-a1b2c3d4e5f6", "a1b2c3d4e5f6")]
    [InlineData("https://linear.app/acme/project/checkout-rewrite-a1b2c3d4e5f6/overview", "a1b2c3d4e5f6")]
    [InlineData("  https://linear.app/acme/project/a1b2c3d4e5f6  ", "a1b2c3d4e5f6")]
    public void A_project_url_gives_the_slug_id_at_the_end_of_its_slug(string url, string slug)
    {
        var parsed = LinearProjectRefs.Parse(url);

        Expect.Equal((null, slug), (parsed.Value.Key, parsed.Value.Slug));
    }

    [Theory]
    [InlineData("")]
    [InlineData("banana")]
    [InlineData("linear:issue/abc-123")]
    [InlineData("linear:project/")]
    [InlineData("http://linear.app/acme/project/checkout-a1b2c3d4e5f6")]
    [InlineData("https://example.com/acme/project/checkout-a1b2c3d4e5f6")]
    [InlineData("https://linear.app/acme/issue/ENG-142")]
    [InlineData("https://linear.app/acme/project/")]
    public void Anything_else_is_SND_PROJECT_004_naming_the_field(string text)
    {
        var parsed = LinearProjectRefs.Parse(text);

        Expect.Equal(SindriErrors.ProjectInvalid.Code, parsed.Error.Code);
        Assert.StartsWith("linear:", parsed.Error.Message, StringComparison.Ordinal);
    }
}
