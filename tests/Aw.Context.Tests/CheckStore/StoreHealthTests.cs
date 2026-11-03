using Aw.Context.Core.Map;

namespace Aw.Context.Tests.CheckStore;

public sealed class StoreHealthTests
{
    private const long MiB = 1024L * 1024;
    private const long GiB = 1024L * MiB;

    private static long Gb(double value) => (long)(value * GiB);

    [Fact, Trait("Lane", "unit")]
    public void No_store_file_yet_is_ok()
    {
        var check = StoreHealth.Check(sizes: null);

        Assert.Equal(("context:store", MapHealthState.Ok, "no store yet"), (check.Name, check.State, check.Detail));
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_small_log_is_ok_and_the_detail_gives_both_sizes()
    {
        var check = StoreHealth.Check(new StoreFileSizes(Gb(2.4), 12 * MiB));

        Assert.Equal(MapHealthState.Ok, check.State);
        Assert.Equal("context.db 2.4 GB, its log 12.0 MB", check.Detail);
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_log_of_13_GB_beside_a_2_4_GB_database_warns_about_a_long_running_reader()
    {
        var check = StoreHealth.Check(new StoreFileSizes(Gb(2.4), Gb(13.16)));

        Assert.Equal(MapHealthState.Warn, check.State);
        Assert.Contains("13.2 GB", check.Detail, StringComparison.Ordinal);
        Assert.Contains("2.4 GB", check.Detail, StringComparison.Ordinal);
        Assert.Contains("long-running reader", check.Detail, StringComparison.Ordinal);
        Assert.Contains("restart Sindri", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_log_over_1_GiB_warns_when_the_database_is_smaller()
    {
        var check = StoreHealth.Check(new StoreFileSizes(100 * MiB, 2 * GiB));

        Assert.Equal(MapHealthState.Warn, check.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_log_over_1_GiB_is_ok_while_it_is_smaller_than_the_database()
    {
        var check = StoreHealth.Check(new StoreFileSizes(3 * GiB, Gb(1.5)));

        Assert.Equal(MapHealthState.Ok, check.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_log_under_1_GiB_is_ok_however_small_the_database_is()
    {
        var check = StoreHealth.Check(new StoreFileSizes(10 * MiB, 900 * MiB));

        Assert.Equal(MapHealthState.Ok, check.State);
    }
}
