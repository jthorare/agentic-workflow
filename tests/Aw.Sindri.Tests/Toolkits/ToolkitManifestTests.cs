using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Tests.Toolkits;

public sealed class ToolkitManifestTests
{
    private const string Good = "{\"name\":\"notes\",\"version\":\"1.0.0\",\"resources\":[{\"kind\":\"disk\",\"name\":\"pad\",\"size\":\"1GiB\",\"variable\":\"SINDRI_DISK_PAD\"}],"
        + "\"tools\":[{\"name\":\"add_note\",\"description\":\"Adds a note\",\"inputSchema\":{\"type\":\"object\"},\"outputSchema\":{\"type\":\"object\"}}]}";

    [Fact, Trait("Lane", "unit")]
    public void The_SDK_manifest_reads_into_the_model()
    {
        var manifest = ToolkitManifest.Parse(Good).Value;

        Assert.Equal("notes", manifest.Name);
        Assert.Equal(new ToolkitResource("disk", "pad", "1GiB", "SINDRI_DISK_PAD"), Assert.Single(manifest.Resources));
        Assert.Equal("add_note", Assert.Single(manifest.Tools).Name);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("not json")]
    [InlineData("{\"name\":\"n\"}")]
    [InlineData("{\"name\":\"n\",\"version\":\"1\",\"resources\":[],\"tools\":[{\"name\":\"Bad\",\"description\":\"d\",\"inputSchema\":{},\"outputSchema\":{}}]}")]
    public void A_manifest_that_is_not_the_SDK_shape_is_SND_TK_003(string json)
    {
        var result = ToolkitManifest.Parse(json);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.ToolkitManifestInvalid.Code, result.Error.Code);
    }
}
