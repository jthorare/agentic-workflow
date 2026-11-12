using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>
/// The "Turn on for your repos" row keeps its first-seen time while it stands (#2938): the needs-you tag hashes the row, so a row stamped with each request's "now" would move the tag every poll and
/// If-None-Match would never get a 304. The service answers 304 when the request's tag equals the computed one, so equal tags are the 304.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class TurnOnMcpTagTests
{
    private static readonly ViewsStatus _current = new(Cursor: 10, LedgerHead: 10, ProjectedAt: KnownInstants.Epoch, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null);

    private readonly SliceHarness _slice = new();
    private readonly Switch _switch = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Needs_you_tag_holds_while_the_turn_on_row_stands_and_the_clock_moves()
    {
        var notices = await Notices();

        var first = await notices.Mcp(Token);
        var firstTag = Tag(first!);
        _slice.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await notices.Mcp(Token);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(firstTag, Tag(second));   // equal tag = the service answers NotModified for If-None-Match: firstTag
    }

    [Fact]
    public async Task Turn_on_row_that_goes_away_and_comes_back_shows_its_new_first_sighting()
    {
        var notices = await Notices();
        var t0 = _slice.Clock.GetUtcNow();
        Assert.Equal(t0, (await notices.Mcp(Token))!.Since);

        _slice.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(t0, (await notices.Mcp(Token))!.Since);   // still standing: the first sighting holds

        _slice.Clock.Advance(TimeSpan.FromSeconds(30));
        _switch.Off = true;
        Assert.Null(await notices.Mcp(Token));

        _slice.Clock.Advance(TimeSpan.FromMinutes(1));
        _switch.Off = false;
        var t2 = _slice.Clock.GetUtcNow();
        var back = await notices.Mcp(Token);

        Assert.Equal(t2, back!.Since);
        Assert.NotEqual(t0, back.Since);
    }

    private static string Tag(NeedsYouRow row) => GetNeedsYouService.TagOf([row], empty: null, _current, SliceHarness.Json);

    private async Task<SetupNotices> Notices()
    {
        _ = await _slice.HoldLease();
        var live = SliceHarness.Profile();
        var profile = live.Profile with { Repos = [new RepoEntry("demo", "/work/demo")] };
        var loaded = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), live.Source);
        _ = await _slice.Profiles.Save(loaded, Token);
        var events = new McpEvents(_slice.Held, _slice.Store, _slice.Events, _slice.Callers);
        var gate = new McpAutoGate(_slice.Profiles, events, _switch, new InMemoryJobViews());
        return new SetupNotices(new InMemoryJobViews(), _slice.Profiles, new PassScrubber(), _slice.Clock, gate, new TurnOnMcpSighting());
    }

    private sealed class Switch : IMcpAutoSwitch
    {
        public bool Off { get; set; }
    }

    private sealed class PassScrubber : ITextScrubber
    {
        public string Scrub(string text) => text;
    }
}
