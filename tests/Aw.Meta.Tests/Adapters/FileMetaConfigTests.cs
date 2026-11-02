using System.Text;
using Aw.Kernel.Json;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Config;

namespace Aw.Meta.Tests.Adapters;

public sealed class FileMetaConfigTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private (FileMetaConfig Config, string Path) Make(string json)
    {
        var path = Path.Combine(_dir.Path, "team.json");
        File.WriteAllText(path, json, Encoding.UTF8);
        return (new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default)), path);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Unknown_fields_are_ignored_and_optional_ones_read()
    {
        var (config, _) = Make(
            """{"team":"acme","repo":"/r","lanes":["a","b"],"prompts":"/p","futureField":{"x":1},"gate":{"always":[],"docsOnly":[],"scopes":{},"alsoNew":true},"mergeOrder":[{"before":1,"after":2,"why":"x"}]}""");

        var loaded = await config.Load(CancellationToken.None);

        Assert.True(loaded.IsSuccess, loaded.IsError ? loaded.Error.Message : string.Empty);
        Assert.Equal(("acme", "/p", "meta"), (loaded.Value.Team, loaded.Value.Prompts, loaded.Value.MetaLane));
        _ = Assert.Single(loaded.Value.MergeOrder);
    }

    private const string Base = """{"team":"acme","repo":"/r","lanes":["a","b"]""";

    [Fact, Trait("Lane", "integration")]
    public async Task A_config_without_the_resource_keys_gets_the_defaults()
    {
        var (config, _) = Make(Base + "}");

        var loaded = (await config.Load(CancellationToken.None)).Value;

        Assert.Equal((new ResourcesConfig(30, 8, 90), new BuildersConfig(MaxPerLane: null, WorktreeGb: 4), true), (loaded.Resources, loaded.Builders, loaded.Heavy.EffectiveReserve.Enabled));
        Assert.Equal([3, 5, 7], loaded.Heavy.EffectiveReserve.Tiers.Select(t => t.MinDepth));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_resource_builder_and_heavy_keys_read_with_their_own_values()
    {
        var (config, _) = Make(Base + ""","resources":{"diskFloorGb":50,"memFloorGb":6},"builders":{"maxPerLane":2,"worktreeGb":9},"heavy":{"reserve":[{"minDepth":2,"slots":1},{"minDepth":4,"slots":"all"}]}}""");

        var loaded = (await config.Load(CancellationToken.None)).Value;

        Assert.Equal((new ResourcesConfig(50, 6, 90), new BuildersConfig(2, 9)), (loaded.Resources, loaded.Builders));
        Assert.Equal([(2, 1), (4, 3)], loaded.Heavy.EffectiveReserve.Tiers.Select(t => (t.MinDepth, t.Slots.Resolve(3))));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public async Task Reserve_false_turns_the_reservation_off_and_true_means_the_defaults(string reserve, bool enabled)
    {
        var (config, _) = Make(Base + ",\"heavy\":{\"reserve\":" + reserve + "}}");

        var loaded = (await config.Load(CancellationToken.None)).Value;

        Assert.Equal(enabled, loaded.Heavy.EffectiveReserve.Enabled);
        Assert.Equal(enabled, loaded.Heavy.EffectiveReserve.Tiers.Count > 0);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("\"reserve\":\"yes\"")]
    [InlineData("\"reserve\":[{\"minDepth\":3}]")]
    [InlineData("\"reserve\":[{\"slots\":1}]")]
    [InlineData("\"reserve\":[{\"minDepth\":3,\"slots\":\"most\"}]")]
    [InlineData("\"reserve\":[{\"minDepth\":-1,\"slots\":1}]")]
    [InlineData("\"reserve\":[3]")]
    public async Task A_reserve_that_is_not_tiers_or_a_boolean_is_AW_META_003(string reserve)
    {
        var (config, _) = Make(Base + ",\"heavy\":{" + reserve + "}}");

        var loaded = await config.Load(CancellationToken.None);

        Assert.Equal(MetaErrors.ConfigInvalid.Code, loaded.Error.Code);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("\"resources\":{\"swapHighPct\":0}")]
    [InlineData("\"resources\":{\"swapHighPct\":101}")]
    [InlineData("\"resources\":{\"diskFloorGb\":-1}")]
    [InlineData("\"resources\":{\"memFloorGb\":-5}")]
    [InlineData("\"builders\":{\"maxPerLane\":0}")]
    [InlineData("\"builders\":{\"worktreeGb\":-4}")]
    [InlineData("\"heavy\":{\"cap\":0}")]
    public async Task A_threshold_outside_its_range_is_AW_META_003(string key)
    {
        var (config, _) = Make(Base + "," + key + "}");

        var loaded = await config.Load(CancellationToken.None);

        Assert.Equal(MetaErrors.ConfigInvalid.Code, loaded.Error.Code);
        Assert.Contains("must be", loaded.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_heavy_cap_reads_with_its_default_of_three()
    {
        var (plain, _) = Make(Base + "}");
        var defaulted = (await plain.Load(CancellationToken.None)).Value.Heavy.Cap;
        var (capped, _) = Make(Base + ",\"heavy\":{\"cap\":5}}");

        Assert.Equal((3, 5), (defaulted, (await capped.Load(CancellationToken.None)).Value.Heavy.Cap));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Agent_absent_is_null_and_a_catalog_id_reads()
    {
        var (plain, _) = Make(Base + "}");
        var absent = (await plain.Load(CancellationToken.None)).Value.Agent;
        var (named, _) = Make(Base + ",\"agent\":\"cursor\"}");

        Assert.Null(absent);
        Assert.Equal("cursor", (await named.Load(CancellationToken.None)).Value.Agent);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("\"agent\":\"\"")]
    [InlineData("\"agent\":\"Cursor\"")]
    [InlineData("\"agent\":\"a\"")]
    [InlineData("\"agent\":1")]
    public async Task An_agent_that_is_not_a_catalog_id_is_AW_META_003(string agent)
    {
        var (config, _) = Make(Base + "," + agent + "}");

        var loaded = await config.Load(CancellationToken.None);

        Assert.Equal(MetaErrors.ConfigInvalid.Code, loaded.Error.Code);
        Assert.Contains("agent", loaded.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_reserve_setting_survives_a_save_and_a_load()
    {
        var (config, _) = Make(Base + "}");
        var team = (await config.Load(CancellationToken.None)).Value with { Heavy = new HeavyConfig(ReserveSetting.Off) };

        _ = await config.Save(team, CancellationToken.None);
        var again = (await config.Load(CancellationToken.None)).Value;

        Assert.False(again.Heavy.EffectiveReserve.Enabled);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not json")]
    [InlineData("{\"team\":\"acme\"}")]
    [InlineData("{\"team\":\"acme\",\"repo\":\"/r\",\"lanes\":\"a\"}")]
    [InlineData("[]")]
    public async Task A_config_that_does_not_parse_is_AW_META_003(string json)
    {
        var (config, _) = Make(json);

        var loaded = await config.Load(CancellationToken.None);

        Assert.True(loaded.IsError);
        Assert.Equal(MetaErrors.ConfigInvalid.Code, loaded.Error.Code);
    }

    public void Dispose() => _dir.Dispose();
}
