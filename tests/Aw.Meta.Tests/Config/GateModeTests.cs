using Aw.Kernel.Json;
using Aw.Meta.Core.Config;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Config;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Config;

public sealed class GateModeTests
{
    private const string Head = """{"team":"t1","repo":"/w","lanes":["meta"]""";

    [Fact, Trait("Lane", "unit")]
    public void A_gate_built_without_a_mode_is_local()
    {
        var gate = GateConfig.Empty;

        Assert.Equal(("local", false), (gate.Mode, gate.IsCi));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("", "local", false)]
    [InlineData(""","gate":{"always":[],"docsOnly":[],"scopes":{}}""", "local", false)]
    [InlineData(""","gate":{"mode":"local"}""", "local", false)]
    [InlineData(""","gate":{"mode":"ci"}""", "ci", true)]
    public async Task The_mode_is_read_from_the_team_file_and_a_file_without_it_reads_local(string gate, string mode, bool ci)
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "team.json");
        var config = new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default));
        await File.WriteAllTextAsync(path, Head + gate + "}", TestContext.Current.CancellationToken);

        var loaded = await config.Load(CancellationToken.None);

        Assert.True(loaded.IsSuccess, loaded.IsError ? loaded.Error.Message : string.Empty);
        Assert.Equal((mode, ci), (loaded.Value.Gate.Mode, loaded.Value.Gate.IsCi));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_ci_mode_survives_a_save_and_a_load()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "team.json");
        var config = new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default));
        var team = new TeamConfig("t1", "/w", ["meta"]) { Gate = GateConfig.Empty with { Mode = "ci" } };

        _ = await config.Save(team, CancellationToken.None);
        var loaded = await config.Load(CancellationToken.None);

        Assert.True(loaded.Value.Gate.IsCi);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("bogus")]
    [InlineData("CI")]
    [InlineData("")]
    public async Task Any_other_mode_is_refused_at_load_as_AW_META_003(string mode)
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "team.json");
        var config = new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default));
        await File.WriteAllTextAsync(path, Head + ",\"gate\":{\"mode\":\"" + mode + "\"}}", TestContext.Current.CancellationToken);

        var loaded = await config.Load(CancellationToken.None);

        Assert.True(loaded.IsError);
        Assert.Equal("AW-META-003", loaded.Error.Code);
        Assert.Contains("gate.mode", loaded.Error.Message, StringComparison.Ordinal);
    }
}
