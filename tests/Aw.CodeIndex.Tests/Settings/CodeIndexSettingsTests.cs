using Aw.CodeIndex.Core;

namespace Aw.CodeIndex.Tests.Settings;

public sealed class CodeIndexSettingsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Create_with_no_keys_gives_the_defaults()
    {
        var result = CodeIndexSettings.Create(input: null);

        Assert.Equal(CodeIndexSettings.Default, result.Value);
        Assert.Equal(4_096, result.Value.MaxFileKB);
        Assert.Equal(4_096, result.Value.MaxTotalMB);
        Assert.True(result.Value.Embeddings.Enabled);
        Assert.Equal("http://127.0.0.1:11434", result.Value.Embeddings.Url);
        Assert.Null(result.Value.Embeddings.Model);
        Assert.Equal(1_024, result.Value.Graph.MaxMegabytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Create_keeps_the_keys_it_is_given_and_defaults_the_rest()
    {
        var input = new CodeIndexSettingsInput(DenyPaths: ["vendor/**"], MaxFileKB: 64, GraphEnabled: false, EmbeddingsUrl: "http://[::1]:11434");

        var settings = CodeIndexSettings.Create(input).Value;

        Assert.Equal(["vendor/**"], settings.DenyPaths);
        Assert.Equal(64, settings.MaxFileKB);
        Assert.Equal(4_096, settings.MaxTotalMB);
        Assert.False(settings.Graph.Enabled);
        Assert.Equal("http://[::1]:11434", settings.Embeddings.Url);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("index.maxFileKB", 0, null, null)]
    [InlineData("index.maxTotalMB", null, -1, null)]
    [InlineData("index.graph.maxMB", null, null, 0)]
    public void Create_names_the_key_path_of_a_bad_number(string keyPath, int? maxFileKb, int? maxTotalMb, int? graphMaxMb)
    {
        var result = CodeIndexSettings.Create(new CodeIndexSettingsInput(MaxFileKB: maxFileKb, MaxTotalMB: maxTotalMb, GraphMaxMB: graphMaxMb));

        Assert.Equal("SND-CODEIDX-001", result.Error.Code);
        Assert.StartsWith(keyPath, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Create_refuses_an_empty_glob()
    {
        var result = CodeIndexSettings.Create(new CodeIndexSettingsInput(UtilityGlobs: ["src/util/**", " "]));

        Assert.Equal("SND-CODEIDX-001", result.Error.Code);
        Assert.StartsWith("index.utilityGlobs", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://localhost:11434")]
    [InlineData("http://192.168.1.5:11434")]
    [InlineData("https://127.0.0.1:11434")]
    [InlineData("http://user:" + "pass@127.0.0.1:11434")]
    [InlineData("not a url")]
    public void Create_refuses_an_embedding_url_that_is_not_plain_loopback(string url)
    {
        var result = CodeIndexSettings.Create(new CodeIndexSettingsInput(EmbeddingsUrl: url));

        Assert.Equal("SND-CODEIDX-005", result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("llama3-cloud")]
    [InlineData("gpt-OSS:120B-Cloud")]
    [InlineData("")]
    public void Create_refuses_a_cloud_or_empty_model(string model)
    {
        var result = CodeIndexSettings.Create(new CodeIndexSettingsInput(EmbeddingsModel: model));

        Assert.Equal("SND-CODEIDX-005", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void SnapshotLimits_turns_the_caps_into_bytes()
    {
        var limits = CodeIndexSettings.Default.SnapshotLimits();

        Assert.Equal(4_096L * 1024, limits.MaxFileBytes);
        Assert.Equal(4_096L * 1024 * 1024, limits.MaxTotalBytes);
    }
}
