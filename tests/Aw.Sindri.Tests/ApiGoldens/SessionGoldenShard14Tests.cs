namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>Shard 14 of <see cref="ApiGoldenTests.SessionShards"/> of the API goldens run as a session, a class of its own so the shards run in parallel (#2534).</summary>
public sealed class SessionGoldenShard14Tests
{
    [Fact, Trait("Lane", "integration")]
    public Task The_API_goldens_of_this_shard_pass_when_run_as_a_session() => ApiGoldenTests.RunSessionShard(14);
}
