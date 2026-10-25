using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>#1102: a challenger that reports the cumulative challenge log ({"rounds":[...]}) as 'fact:challengeRound' is told what is wrong with its shape, not that its verdict is 'none'.</summary>
public sealed class ChallengeRoundShapeTests
{
    private const string Log = """{"rounds":[{"walked":[{"kind":"lens","name":"code"}],"verdict":"found","behaviours":[{"text":"list --file with no path exits 1","question":"Should it exit 2?","citations":["L-code-2"]}],"round":1}]}""";

    [Fact, Trait("Lane", "unit")]
    public async Task Challenge_log_names_the_shape_when_the_round_is_a_challenge_log()
    {
        var facts = new ScopingFacts { ["challenge"] = Log, ["challengeRound"] = Log };

        var result = await new ScopingDeskService().Handle(new ScopingDeskRequest("challenge-log", facts), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("fact:challengeRound is a challenge log, not a round", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("verdict 'none'", result.Error.Message, StringComparison.Ordinal);
    }
}
