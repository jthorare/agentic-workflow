using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Meta.Core.Config;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Config;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Config;

// #3789: review.mode is "ok" (the default) or "counts"; any other value is a config error.
public sealed class ReviewModeTests
{
    private const string Head = """{"team":"t1","repo":"/w","lanes":["meta","ideas"]""";

    private static TeamConfig Team(string mode) => new("t1", "/w", ["meta", "ideas"]) { Review = new ReviewConfig("ideas", mode) };

    [Fact, Trait("Lane", "unit")]
    public void A_review_lane_built_without_a_mode_needs_the_OK()
    {
        var review = new ReviewConfig("ideas");

        Assert.Equal(("ok", false), (review.Mode, review.IsCounts));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ok")]
    [InlineData("counts")]
    public void Ok_and_counts_are_valid(string mode) =>
        Assert.Null(ThresholdChecks.Problem(Team(mode)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("bogus")]
    [InlineData("COUNTS")]
    [InlineData("")]
    public void Any_other_mode_is_a_config_problem(string mode) =>
        Assert.Equal($"review.mode must be \"ok\" or \"counts\", not \"{mode}\"", ThresholdChecks.Problem(Team(mode)));

    [Theory, Trait("Lane", "integration")]
    [InlineData("""{"lane":"ideas"}""", "ok", false)]
    [InlineData("""{"lane":"ideas","mode":"ok"}""", "ok", false)]
    [InlineData("""{"lane":"ideas","mode":"counts"}""", "counts", true)]
    public async Task The_mode_is_read_from_the_team_file_and_a_file_without_it_reads_ok(string review, string mode, bool counts)
    {
        var loaded = await Load(review);

        Assert.True(loaded.IsSuccess, loaded.IsError ? loaded.Error.Message : string.Empty);
        Assert.Equal((mode, counts), (loaded.Value.Review?.Mode, loaded.Value.Review?.IsCounts ?? false));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_mode_is_refused_at_load_as_AW_META_003()
    {
        var loaded = await Load("""{"lane":"ideas","mode":"fast"}""");

        Assert.True(loaded.IsError);
        Assert.Equal("AW-META-003", loaded.Error.Code);
        Assert.Contains("review.mode", loaded.Error.Message, StringComparison.Ordinal);
    }

    private static async Task<Result<TeamConfig>> Load(string review)
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "team.json");
        var config = new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default));
        await File.WriteAllTextAsync(path, Head + ",\"review\":" + review + "}", TestContext.Current.CancellationToken);
        return await config.Load(CancellationToken.None);
    }
}
