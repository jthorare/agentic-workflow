namespace Aw.Sindri.Tests.Catalog;

/// <summary>Shard 8 of <see cref="WriteAttributionTests.Shards"/> of the launched-session write sweep, a class of its own so the shards run in parallel (#2534).</summary>
public sealed class WriteAttributionShard8Tests
{
    [Fact, Trait("Lane", "integration")]
    public Task Every_write_a_launched_session_makes_in_this_shard_records_that_launch_as_its_actor() => WriteAttributionTests.RunShard(8);
}
