using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Items;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.GetItem;

/// <summary>One item (API spec §3.1): its version for a close, the actions its state offers, and the body only here (U2-API-7).</summary>
[Lane(TestLane.Unit)]
public sealed class GetItemTests
{
    private readonly ItemQueryHarness _q = new();

    private GetItemService Service() => new(new ViewItemResolver(_q.Views.Views), _q.Views.Progress, _q.Views.Slice.Callers, new EmptyLaunchSource(), _q.Views.Views);

    [Fact]
    public async Task GetItem_shows_the_item_with_its_version_and_actions()
    {
        await _q.Item('1', "Ship it", repo: "web");
        using var entered = _q.Human();

        var result = await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var item = result.Value;
        Expect.Equal(("Ship it", 1L, ItemState.New), (item.Title, item.Version, item.State));
        Expect.Equal(RepoName.From("web"), item.Repo);
        Expect.Sequence(ItemActions.For(ItemState.New), [.. item.Actions]);
        Expect.Equal(_q.Views.Progress.Current.AsOf, item.AsOf);
    }

    [Fact]
    public async Task Item_actions_follow_its_state()
    {
        await _q.Item('1', "finished", state: ItemState.Done);
        using var entered = _q.Human();

        var item = (await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token)).Value;

        Assert.Empty(item.Actions);
        Assert.Null(item.PrimaryAction);
    }

    [Fact]
    public async Task Body_and_link_only_in_GetItem()
    {
        await _q.Item('1', "Ship it");
        using var entered = _q.Human();

        var item = (await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token)).Value;

        Expect.Equal("the body", item.Body);
        var fields = typeof(ItemSummary).GetProperties().Select(static p => p.Name).ToList();
        Assert.DoesNotContain("Body", fields, StringComparer.Ordinal);
        Assert.DoesNotContain("Link", fields, StringComparer.Ordinal);
        Assert.DoesNotContain("Version", fields, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Session_outside_the_repo_sees_the_item_like_a_human()
    {
        await _q.Item('1', "secret", repo: "alpha");
        using var entered = _q.Session("beta");

        var result = await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token);

        Expect.Equal("secret", result.Value.Title);
    }

    [Fact]
    public async Task Item_lists_its_runs_newest_first()
    {
        await _q.Item('1', "Ship it");
        await Link('1', "sindri:run/nightly@1:1");
        _q.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        await Link('1', "sindri:run/nightly@1:2");
        await Link('1', "github:pr/acme-widgets-12");   // not a run
        using var entered = _q.Human();

        var item = (await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token)).Value;

        Expect.Sequence(["nightly@1:2", "nightly@1:1"], [.. item.Runs]);
    }

    [Fact]
    public async Task Item_without_runs_has_an_empty_run_list()
    {
        await _q.Item('1', "Ship it");
        await _q.Item('2', "Other");
        await Link('2', "sindri:run/nightly@1:1");
        using var entered = _q.Human();

        var item = (await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token)).Value;

        Assert.Empty(item.Runs);
    }

    [Fact]
    public async Task Item_read_fails_when_the_item_links_view_is_unreadable()
    {
        await _q.Item('1', "Ship it");
        using var entered = _q.Human();
        var service = new GetItemService(new ViewItemResolver(_q.Views.Views), _q.Views.Progress, _q.Views.Slice.Callers, new EmptyLaunchSource(), new LinksUnreadable(_q.Views.Views));

        var result = await service.Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token);

        Assert.True(result.IsError);   // never an empty Runs: an agent can't tell "no runs" from "views broken"
    }

    /// <summary>The real reader for every view but 'item-links', whose reads fail.</summary>
    private sealed class LinksUnreadable(IViewReader inner) : IViewReader
    {
        private static Result<T> Fail<T>() => SindriErrors.ViewsStalled.With("item-links can't be read (fixture)");

        public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
            where TRow : class => view == ViewNames.ItemLinks ? ValueTask.FromResult(Fail<TRow?>()) : inner.Get<TRow>(view, key, cancellationToken);

        public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
            where TRow : class => view == ViewNames.ItemLinks ? ValueTask.FromResult(Fail<IReadOnlyList<TRow>>()) : inner.List<TRow>(view, cancellationToken);

        public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
            where TRow : class => view == ViewNames.ItemLinks ? ValueTask.FromResult(Fail<IReadOnlyList<TRow>>()) : inner.Range<TRow>(view, afterKey, beforeKey, max, cancellationToken);

        public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
            where TRow : class => view == ViewNames.ItemLinks ? ValueTask.FromResult(Fail<IReadOnlyList<TRow>>()) : inner.Where<TRow>(view, column, value, cancellationToken);

        public ValueTask<Result<string>> Mark(string view, CancellationToken cancellationToken) => inner.Mark(view, cancellationToken);

        public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) => inner.State(cancellationToken);
    }

    [Fact]
    public async Task An_invalid_live_profile_reads_the_item_like_no_profile()
    {
        await _q.Item('1', "Ship it", repo: "web");
        using var entered = _q.Human();
        var slice = _q.Views.Slice;
        var real = new ItemLaunchSource(new LaunchViews(_q.Views.Views), new LaunchTargetsReader(slice.Store, slice.Events, slice.Profiles, new FixedLaunchSpend(), _q.Views.Progress), slice.Profiles, new AgentCatalog(SessionData.Agents), slice.Clock);
        var service = new GetItemService(new ViewItemResolver(_q.Views.Views), _q.Views.Progress, slice.Callers, real, _q.Views.Views);
        slice.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var result = await service.Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal("Ship it", result.Value.Title);
    }

    private async Task Link(char digit, string record) =>
        await _q.Views.Fold(EventTypes.ItemLinked, new ItemLinked(_q.Views.Slice.Ids.New<CommandId>(), ItemQueryHarness.Id(digit), RecordKey.From(record), Alias: null), ItemQueryHarness.Id(digit));

    /// <summary>The launch block is decided elsewhere (LaunchOptionsTests); here an item only has to carry it.</summary>
    private sealed class EmptyLaunchSource : IItemLaunchSource
    {
        public ValueTask<Result<ItemLaunch>> For(ItemRow item, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ItemLaunch>>(new ItemLaunch(Modes: [], AgentId.From("claude-code"), ChosenBy: "builtIn", Attach: null, Resumable: null, InFlight: null, LastStarted: null, Confirm: null, RepoPath: null, Blockers: []));
    }
}
