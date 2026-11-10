using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.Inbox;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Inbox;

/// <summary>`inbox --once`, what the meta lane's hook runs (#2498): nothing when nothing is open, each open message once.</summary>
public sealed class InboxOnceTests
{
    private readonly MetaWorld _world = new();

    private ListInboxService List() => new(_world.Config, _world.Messages, _world.Reader, _world.Clock);

    private async Task Receive(params (string Id, string Name, string Body)[] messages)
    {
        _world.Transcripts.Write(MetaWorld.Transcript, string.Concat(messages.Select(m => MetaWorld.PeerLine(m.Id, m.Name, m.Body))));
        _ = await _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None);
    }

    private async Task<string[]> Once()
    {
        var result = await List().Handle(new ListInboxRequest(All: false, Once: true), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return [.. result.Value.Items.Select(i => i.Id.Value)];
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_nothing_open_once_lists_nothing()
    {
        await _world.Ready();

        Assert.Empty(await Once());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_message_is_listed_by_the_first_once_and_not_by_the_second()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "please rebase"));

        var first = await Once();
        var second = await Once();

        Assert.Equal(["m1"], first);
        Assert.Empty(second);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_message_that_arrives_later_is_the_only_one_listed_next()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "please rebase"));
        _ = await Once();
        await Receive(("m2", "t1-ui", "ready #190 079c2541"));

        Assert.Equal(["m2"], await Once());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_plain_list_still_shows_a_message_once_already_showed()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "please rebase"));
        _ = await Once();

        var plain = await List().Handle(new ListInboxRequest(All: false), CancellationToken.None);

        Assert.Equal(["m1"], plain.Value.Items.Select(i => i.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deferred_message_is_listed_once_more_when_its_deferral_runs_out()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "please rebase"));
        _ = await Once();
        var until = _world.Clock.GetUtcNow().AddHours(1);
        _ = await _world.Messages.Resolve(MessageId.From("m1"), new(MessageResolutionKind.Deferred, "later", until), CancellationToken.None);

        var during = await Once();
        _world.Clock.Advance(TimeSpan.FromHours(2));
        var after = await Once();
        var again = await Once();

        Assert.Empty(during);
        Assert.Equal(["m1"], after);
        Assert.Empty(again);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dismissed_message_is_never_listed()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "fyi only"));
        _ = await _world.Messages.Resolve(MessageId.From("m1"), new(MessageResolutionKind.Dismissed, "fyi"), CancellationToken.None);

        Assert.Empty(await Once());
    }
}
