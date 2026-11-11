using Aw.Meta.Core.Coordination;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneRecycle;

// #2683: the one resolver for the meta's live transcript. No reader may find it through the offsets or role rows alone.
public sealed class MetaTranscriptResolverTests
{
    private readonly MetaWorld _world = new();

    private MetaTranscriptResolver Resolver() => new(_world.Messages, _world.Sessions, new InMemoryTranscriptLocator(_world.Transcripts));

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_role_session_resolves_to_its_file_not_to_a_stale_offsets_row()
    {
        await _world.Ready(); // role: s-meta
        _world.Sessions.Add(A.Live("t1-meta", MetaWorld.MetaSessionId));
        _world.Transcripts.Append("/old/place/other.jsonl", string.Empty);
        _ = await _world.Messages.RecordInbound("/old/place/other.jsonl", 0, [], CancellationToken.None); // an offsets row on another path
        _world.Transcripts.Append("/transcripts/" + MetaWorld.MetaSessionId + ".jsonl", string.Empty);

        var found = await Resolver().Find(CancellationToken.None);

        Assert.Equal("/transcripts/" + MetaWorld.MetaSessionId + ".jsonl", found.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_role_id_resolves_to_the_one_live_session_that_carries_the_name()
    {
        await _world.Ready(); // role: s-meta, t1-meta
        _world.Sessions.Add(A.Live("t1-api", "s-api"));
        _world.Sessions.Add(A.Live("t1-meta", "s-meta-2"));
        _world.Transcripts.Append("/transcripts/s-api.jsonl", string.Empty);
        _world.Transcripts.Append("/transcripts/s-meta.jsonl", string.Empty);
        _world.Transcripts.Append("/transcripts/s-meta-2.jsonl", string.Empty);

        var found = await Resolver().Find(CancellationToken.None);

        Assert.Equal("/transcripts/s-meta-2.jsonl", found.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_registered_meta_resolves_to_nothing()
    {
        var found = await Resolver().Find(CancellationToken.None);

        Assert.True(found.IsSuccess);
        Assert.Null(found.Value);
    }
}
