using Aw.Context.Core.Linear;

namespace Aw.Context.Tests.Linear;

/// <summary>Whether a comment mentions a person (Linear hub spec 2026-11-04 §4.3; the syntax is an assumption Ph0's probe checks).</summary>
[Trait("Lane", "unit")]
public sealed class LinearMentionsTests
{
    [Theory]
    [InlineData("ping @ada please look", "ada")]
    [InlineData("@Ada first", "ada")]
    [InlineData("thanks, @ADA.", "ada")]
    [InlineData("see https://linear.app/acme/profiles/ada for her view", "ada")]
    [InlineData("@Ada Fixture can you check", "Ada Fixture")]
    public void A_mention_of_a_name_in_either_spelling_counts(string body, string name) =>
        Assert.True(LinearMentions.Mentions(body, [name]));

    [Theory]
    [InlineData("no one here", "ada")]
    [InlineData("ping @adam please", "ada")]
    [InlineData("email me at ada@example.org", "ada")]
    [InlineData("ada was here", "ada")]
    [InlineData("a path /profiles/adam is not hers", "ada")]
    [InlineData("@ada_two is someone else", "ada")]
    [InlineData("@Joi-Smith is someone else", "Joi")]
    [InlineData("a path /profiles/joi-smith is not hers", "joi")]
    public void A_name_that_is_only_part_of_a_longer_word_or_has_no_marker_does_not(string body, string name) =>
        Assert.False(LinearMentions.Mentions(body, [name]));

    [Fact]
    public void Any_of_several_spellings_counts_and_blank_ones_are_ignored() =>
        Assert.True(LinearMentions.Mentions("hey @adal", [null, "", "  ", "Ada Fixture", "adal"]));

    [Theory]
    [InlineData("hey @joi-smith and @joi", "joi")]
    [InlineData("hey @Joi, look", "joi")]
    [InlineData("hey @Joi - look", "joi")]
    public void A_plain_mention_is_found_after_a_hyphenated_name_and_punctuation_or_a_spaced_dash_still_ends_one(string body, string name) =>
        Assert.True(LinearMentions.Mentions(body, [name]));

    [Fact]
    public void No_names_mention_no_one() =>
        Assert.False(LinearMentions.Mentions("hey @ada", []));

    [Fact]
    public void A_later_occurrence_is_found_after_an_earlier_partial_one() =>
        Assert.True(LinearMentions.Mentions("@adam and then @ada", ["ada"]));
}
