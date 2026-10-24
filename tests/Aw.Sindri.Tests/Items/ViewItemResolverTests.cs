using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Items;

/// <summary>An item reference through the views (API spec §3.1): an id or an alias, then the session's repo scope.</summary>
[Lane(TestLane.Unit)]
public sealed class ViewItemResolverTests
{
    private static readonly RecordKey _record = RecordKey.From("linear:issue/abc-123");
    private static readonly ItemAlias _alias = ItemAlias.From("ENG-142");
    private readonly ViewsHarness _h = new();
    private readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));

    [Fact]
    public async Task Alias_resolves_to_its_item()
    {
        await Seed(repo: null);
        await _h.Fold(EventTypes.ItemLinked, new ItemLinked(_h.Slice.Ids.New<CommandId>(), _item, _record, _alias));
        var caller = new CallerContextBuilder().Human().Build();

        var byAlias = await Resolver().Resolve(ItemRef.To(_alias), caller, TestContext.Current.CancellationToken);
        var byId = await Resolver().Resolve(ItemRef.To(_item), caller, TestContext.Current.CancellationToken);

        Expect.Equal(_item, byAlias.Value.Id);
        Expect.Equal(_item, byId.Value.Id);
    }

    [Fact]
    public async Task Unknown_alias_is_SND_ITEM_404()
    {
        await Seed(repo: null);

        var result = await Resolver().Resolve(ItemRef.To(ItemAlias.From("ENG-999")), new CallerContextBuilder().Human().Build(), TestContext.Current.CancellationToken);

        Expect.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Unknown_id_is_SND_ITEM_404()
    {
        await Seed(repo: null);

        var result = await Resolver().Resolve(ItemRef.To(ItemId.From("ITM-" + new string('9', 32))), new CallerContextBuilder().Human().Build(), TestContext.Current.CancellationToken);

        Expect.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Item_in_another_repo_resolves_for_a_session()
    {
        await Seed(RepoName.From("alpha"));

        var other = await Resolver().Resolve(ItemRef.To(_item), new CallerContextBuilder().Session().InRepo("beta").Build(), TestContext.Current.CancellationToken);
        var same = await Resolver().Resolve(ItemRef.To(_item), new CallerContextBuilder().Session().InRepo("alpha").Build(), TestContext.Current.CancellationToken);
        var human = await Resolver().Resolve(ItemRef.To(_item), new CallerContextBuilder().Human().Build(), TestContext.Current.CancellationToken);

        Expect.Equal(_item, other.Value.Id);
        Expect.Equal(_item, same.Value.Id);
        Expect.Equal(_item, human.Value.Id);
    }

    private ViewItemResolver Resolver() => new(_h.Views);

    private Task Seed(RepoName? repo) =>
        _h.Fold(EventTypes.ItemCreated, new ItemCreated(_h.Slice.Ids.New<CommandId>(), _item, "Ship it", repo, Link: null, Body: null, Owner: null));
}
