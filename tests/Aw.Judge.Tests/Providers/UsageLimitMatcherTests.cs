using Aw.Judge.Infrastructure.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class UsageLimitMatcherTests
{
    public static TheoryData<string> Real => UsageLimitStrings.AllData;

    [Theory]
    [MemberData(nameof(Real))]
    public void Each_real_limit_string_matches(string message) => Assert.NotNull(UsageLimitMatcher.Match(message));

    [Theory]
    [InlineData("  \n You've hit your session limit · resets 1am (UTC)")]
    [InlineData("⎿  You've hit your weekly limit · resets Oct 20, 3am (UTC)")]
    [InlineData("You’ve hit your session limit · resets 5pm (UTC)")]
    public void Leading_blanks_and_marks_do_not_stop_a_match(string message) => Assert.NotNull(UsageLimitMatcher.Match(message));

    [Theory]
    [InlineData("")]
    [InlineData("The rate limit for this endpoint is 10 per minute")]
    [InlineData("That limit is a good one")]
    [InlineData("Error: invalid_json_schema")]
    [InlineData("You've hit a snag")]
    public void Other_text_is_not_a_limit(string message) => Assert.Null(UsageLimitMatcher.Match(message));

    [Fact]
    public void Null_is_not_a_limit() => Assert.Null(UsageLimitMatcher.Match(message: null));

    [Theory]
    [MemberData(nameof(Real))]
    public void A_limit_string_in_the_middle_of_a_message_is_not_a_limit(string message) =>
        Assert.Null(UsageLimitMatcher.Match($"Failed on the prompt: please summarise /repo/docs/{message}"));

    [Theory]
    [MemberData(nameof(Real))]
    public void A_limit_string_on_a_later_line_is_not_a_limit(string message) =>
        Assert.Null(UsageLimitMatcher.Match($"Something else went wrong\n{message}"));

    [Fact]
    public void The_pattern_id_says_which_one_fired()
    {
        var ids = UsageLimitStrings.All.Select(UsageLimitMatcher.Match).ToList();

        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.True(ids.Distinct(StringComparer.Ordinal).Skip(1).Any(), "the Claude and Codex strings fire different patterns");
    }
}
