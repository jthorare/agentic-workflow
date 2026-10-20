using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Features.MapHeads;
using Aw.Context.Testing.Extract;
using Aw.Context.Tests.BuildMap;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.MapHeads;

[Trait("Lane", "unit")]
public sealed class MapHeadsTests
{
    private const string SplitHead = "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567";
    private const string OtherHead = "1111111111111111111111111111111111111111";
    private static readonly ContextRepo _split = MapWorld.Repos[0];
    private static readonly ContextRepo _other = MapWorld.Repos[1];

    private readonly FakeDefaultBranch _defaults = new();

    private static CancellationToken None => TestContext.Current.CancellationToken;

    private MapHeadsService Service() => new(
        new RoutingRepoReader(new Dictionary<string, IRepoReader>(StringComparer.Ordinal)
        {
            [_split.Name.Value] = new FakeRepoReader(_split, SplitHead, new Dictionary<string, string>(StringComparer.Ordinal)),
            [_other.Name.Value] = new FakeRepoReader(_other, OtherHead, new Dictionary<string, string>(StringComparer.Ordinal)),
        }),
        _defaults);

    private ValueTask<Kernel.Result<IReadOnlyList<RepoHead>>> Heads(ContextScope scope) => Service().Handle(new MapHeadsRequest(scope), None);

    [Fact]
    public async Task Each_mapped_repo_answers_its_head_and_whether_the_default_branch_holds_it()
    {
        _ = _defaults.Reach(_split.Name, SplitHead);   // other's checkout is on a feature branch

        var heads = (await Heads(MapWorld.Human)).Value;

        Assert.Equal([new RepoHead(_split.Name, SplitHead, OnDefault: true), new RepoHead(_other.Name, OtherHead, OnDefault: false)], heads);
    }

    [Fact]
    public async Task Only_the_repos_the_settings_map_are_read()
    {
        var heads = (await Heads(MapWorld.HumanWith(MapWorld.OnlyFixtureRepo))).Value;

        Assert.Equal([_split.Name], heads.Select(h => h.Repo));
    }

    [Fact]
    public async Task No_mapped_repo_is_an_empty_list_not_an_error()
    {
        var heads = await Heads(MapWorld.HumanWith("""{"repos":[]}"""));

        Assert.True(heads.IsSuccess);
        Assert.Empty(heads.Value);
    }

    [Fact]
    public async Task Invalid_settings_answer_their_own_error()
    {
        var heads = await Heads(MapWorld.HumanWith("""{"repos":["nowhere"]}"""));

        Assert.Equal(ContextErrors.SettingsInvalid.Code, heads.Error.Code);
    }

    [Fact]
    public async Task A_session_is_refused_like_a_build()
    {
        var heads = await Heads(MapWorld.Session(MapWorld.Repos[0].Name.Value));

        Assert.Equal(ContextErrors.SessionsDisabled.Code, heads.Error.Code);
    }

    [Fact]
    public async Task An_unreadable_checkout_is_its_own_error()
    {
        _ = _defaults.Unreadable(_other.Name);

        var heads = await Heads(MapWorld.Human);

        Assert.Equal(ContextErrors.ExtractionUnreadable.Code, heads.Error.Code);
    }
}
