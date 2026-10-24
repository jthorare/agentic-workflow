using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CodeIndexJobs;
using Aw.Sindri.Features.CodeIndexReads;
using ProfileEmbeddings = Aw.Sindri.Core.Profile.EmbeddingSettings;
using ProfileGraph = Aw.Sindri.Core.Profile.GraphSettings;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>The profile's whole 'index:' section reaches the code index, in the read scope and the job scope alike (removed per owner q93, 2026-10-23: every key was validated and ignored).</summary>
public sealed class CodeIndexProfileSettingsTests
{
    private static SindriProfile Profile() => new(
        1,
        "thor",
        Index: new IndexSettings(
            DenyPaths: ["private/**"],
            UtilityGlobs: ["**/util/**"],
            MaxFileKB: 4096,
            MaxTotalMB: 2048,
            Embeddings: new ProfileEmbeddings(Enabled: false, Url: "http://127.0.0.1:11500", Model: "mxbai-embed-large"),
            Graph: new ProfileGraph(Enabled: false, MaxMB: 1024)),
        Repos:
        [
            new RepoEntry("demo", "/work/demo"),
            new RepoEntry("big", "/work/big", new IndexSettings(DenyPaths: ["gen/**"], MaxFileKB: 8192, Graph: new ProfileGraph(MaxMB: 2048))),
        ]);

    [Fact, Trait("Lane", "unit")]
    public void The_read_scope_hands_the_code_index_every_profile_key()
    {
        var scope = CodeIndexScopes.For(Profile(), caller: null).Value;

        var demo = scope.Repos.Single(r => r.Name.Value == "demo").Settings;
        Assert.Equal(4096, demo.MaxFileKB);
        Assert.Equal(2048, demo.MaxTotalMB);
        Assert.Equal(["**/util/**"], demo.UtilityGlobs);
        Assert.False(demo.Embeddings.Enabled);
        Assert.Equal("http://127.0.0.1:11500", demo.Embeddings.Url);
        Assert.Equal("mxbai-embed-large", demo.Embeddings.Model);
        Assert.False(demo.Graph.Enabled);
        Assert.Equal(1024, demo.Graph.MaxMegabytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repos_scalar_replaces_the_profiles_and_its_list_keys_join()
    {
        var big = CodeIndexJobScopes.For(Profile(), caller: null).Value.Repos.Single(r => r.Name.Value == "big").Settings;

        Assert.Equal(8192, big.MaxFileKB);
        Assert.Equal(2048, big.MaxTotalMB);
        Assert.Equal(2048, big.Graph.MaxMegabytes);
        Assert.Equal(["private/**", "gen/**"], big.DenyPaths);
        Assert.False(big.Embeddings.Enabled);
    }
}
