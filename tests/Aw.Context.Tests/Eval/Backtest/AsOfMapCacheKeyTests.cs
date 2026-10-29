using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class AsOfMapCacheKeyTests
{
    private const string Commit = "0123456789abcdef0123";
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    [Fact, Trait("Lane", "unit")]
    public void An_empty_recognizer_set_leaves_the_key_as_it_was()
    {
        Assert.Equal(AsOfMapCache.Key(_repo, Commit, MapExtractor.Version, clustered: true), AsOfMapCache.Key(_repo, Commit, MapExtractor.Version, clustered: true, RecognizerSet.None), StringComparer.Ordinal);
        Assert.Equal("aw-split-panes-0123456789ab-x5-c1", AsOfMapCache.Key(_repo, Commit, MapExtractor.Version, clustered: true, RecognizerSet.From([])), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recognizer_set_adds_its_hash_to_the_key_and_a_different_set_has_another_key()
    {
        var flags = RecognizerSet.From([new ProfileRecognizer("flag", "config/*.yaml", "rollout: (?<name>[a-z-]+)", "name")]);
        var other = RecognizerSet.From([new ProfileRecognizer("flag", "config/*.yaml", "toggle: (?<name>[a-z-]+)", "name")]);

        var key = AsOfMapCache.Key(_repo, Commit, MapExtractor.Version, clustered: false, flags);

        Assert.Equal($"aw-split-panes-0123456789ab-x5-c0-{flags.Hash}", key, StringComparer.Ordinal);
        Assert.NotEqual(key, AsOfMapCache.Key(_repo, Commit, MapExtractor.Version, clustered: false, other), StringComparer.Ordinal);
    }
}
