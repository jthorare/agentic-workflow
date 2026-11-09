using Aw.Kernel.Testing;

namespace Aw.Sindri.Toolkit.Tests;

public sealed class ToolkitDefinitionTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_toolkit_serializes_its_name_needs_and_tool_schemas_to_the_manifest() =>
        Golden.Match(SampleToolkit.Create().Manifest(), typeof(ToolkitDefinitionTests).Assembly, "Golden/toolkit.json", Normalization.None);

    [Fact, Trait("Lane", "unit")]
    public void A_resource_names_its_environment_variable_like_PR_14()
    {
        Assert.Equal("SINDRI_DB_CACHE", Resources.Database("cache").Variable);
        Assert.Equal("SINDRI_DISK_SCRATCH_1", Resources.Disk("scratch-1", "1GiB").Variable);
        Assert.Equal("SINDRI_CONNECTION_GITHUB", Resources.Connection("github").Variable);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tool_added_before_the_JSON_context_is_refused_with_the_declaration_code()
    {
        var toolkit = Toolkit.Named("early");

        var failure = Assert.Throws<ToolkitException>(() =>
            toolkit.Tool<DiffRequest, DiffResponse>("t", "d", (_, _, _) => Task.FromResult(new DiffResponse("", 0))));

        Assert.Equal(ToolkitErrors.InvalidDeclaration, failure.Definition);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_tools_with_one_name_are_refused()
    {
        var toolkit = SampleToolkit.Create();

        var failure = Assert.Throws<ToolkitException>(() =>
            toolkit.Tool<DiffRequest, DiffResponse>("explode", "d", (_, _, _) => Task.FromResult(new DiffResponse("", 0))));

        Assert.Equal(ToolkitErrors.InvalidDeclaration, failure.Definition);
        Assert.Contains("explode", failure.Message, StringComparison.Ordinal);
    }
}
