using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The code index's 'index:' keys (code index spec §13): limits, utility globs, 'embeddings' and 'graph', each bad one named by its key path.</summary>
public sealed class IndexCodeIndexKeysTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_every_code_index_key_at_the_top_and_on_a_repo()
    {
        _files.Write(Base + "index:\n  utilityGlobs: [\"src/util/**\"]\n  maxFileKB: 256\n  maxTotalMB: 100\n  embeddings: { enabled: true, url: \"http://127.0.0.1:11434\", model: nomic-embed-text }\n  graph: { enabled: false, maxMB: 64 }\n"
            + "repos:\n  - { name: web, path: /work/web, index: { graph: { maxMB: 8 } } }\n");

        var profile = (await _files.Source.Load(Token)).Value.Profile;

        Assert.Equal(["src/util/**"], profile.Index!.UtilityGlobs);
        Assert.Equal(256, profile.Index.MaxFileKB);
        Assert.Equal(100, profile.Index.MaxTotalMB);
        Assert.Equal(new EmbeddingSettings(Enabled: true, Url: "http://127.0.0.1:11434", Model: "nomic-embed-text"), profile.Index.Embeddings);
        Assert.Equal(new GraphSettings(Enabled: false, MaxMB: 64), profile.Index.Graph);
        Assert.Equal(new GraphSettings(MaxMB: 8), profile.Repos![0].Index!.Graph);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("index:\n  maxFile: 1\n", "maxFile")]
    [InlineData("index:\n  embeddings: { enable: true }\n", "enable")]
    [InlineData("index:\n  graph: { maxEdges: 1 }\n", "maxEdges")]
    public async Task An_unknown_key_in_the_section_or_a_nested_record_is_refused_by_name(string yaml, string name)
    {
        _files.Write(Base + yaml);

        var result = await _files.Source.Load(Token);

        Assert.False(result.IsSuccess);
        Assert.Contains(name, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, null, null, "index.maxFileKB: must be 1 to 102400")]
    [InlineData(102401, null, null, "index.maxFileKB: must be 1 to 102400")]
    [InlineData(null, 0, null, "index.maxTotalMB: must be 1 to 102400")]
    [InlineData(null, null, 0, "index.graph.maxMB: must be 1 to 4096")]
    [InlineData(null, null, 4097, "index.graph.maxMB: must be 1 to 4096")]
    public void A_limit_out_of_range_names_its_key_path(int? fileKb, int? totalMb, int? graphMb, string message)
    {
        var index = new IndexSettings(MaxFileKB: fileKb, MaxTotalMB: totalMb, Graph: graphMb is null ? null : new GraphSettings(MaxMB: graphMb));

        var error = ProfileRules.Check(new SindriProfile(1, "thor", Index: index)).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://localhost:11434")]
    [InlineData("http://10.0.0.5:11434")]
    [InlineData("https://ollama.com")]
    [InlineData("http://user:" + "pw@127.0.0.1:11434")]   // split so the pre-commit scrubber (credentialed-url) doesn't read the test input as a secret
    [InlineData("not a url")]
    [InlineData("")]
    public void An_embedding_url_that_is_not_loopback_ip_http_is_refused(string url)
    {
        var index = new IndexSettings(Embeddings: new EmbeddingSettings(Url: url));

        var error = ProfileRules.Check(new SindriProfile(1, "thor", Index: index)).Error;

        Assert.Contains("index.embeddings.url:", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://[::1]:11434")]
    public void A_loopback_embedding_url_is_accepted(string url) =>
        Assert.True(ProfileRules.Check(new SindriProfile(1, "thor", Index: new IndexSettings(Embeddings: new EmbeddingSettings(Url: url)))).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("qwen3-embedding:cloud")]
    [InlineData("Some-Cloud-Model")]
    [InlineData("")]
    public void A_cloud_or_empty_embedding_model_is_refused(string model)
    {
        var error = ProfileRules.Check(new SindriProfile(1, "thor", Index: new IndexSettings(Embeddings: new EmbeddingSettings(Model: model)))).Error;

        Assert.Contains("index.embeddings.model:", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repos_own_bad_key_and_bad_utility_glob_name_the_repo()
    {
        var bad = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web", new IndexSettings(MaxTotalMB: -1))]);
        var glob = new SindriProfile(1, "thor", Index: new IndexSettings(UtilityGlobs: ["ok/**", "../x"]));

        Assert.Contains("repos.web.index.maxTotalMB: must be 1 to 102400", ProfileRules.Check(bad).Error.Message, StringComparison.Ordinal);
        Assert.Contains("index.utilityGlobs[1]: must not contain '..'", ProfileRules.Check(glob).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Defaults_and_a_plain_valid_section_pass()
    {
        Assert.True(ProfileRules.Check(new SindriProfile(1, "thor", Index: new IndexSettings())).IsSuccess);
        Assert.Equal(4_096, IndexSettings.DefaultMaxFileKB);
        Assert.Equal(4_096, IndexSettings.DefaultMaxTotalMB);
        Assert.Equal(1_024, GraphSettings.DefaultMaxMB);
        Assert.Equal("http://127.0.0.1:11434", EmbeddingSettings.DefaultUrl);
    }
}
