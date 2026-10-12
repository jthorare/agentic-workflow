using System.Globalization;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Redaction.Tests;

/// <summary>Extra patterns that break Redaction's pattern rules are refused with a typed error naming the entry and
/// the field (context spec §14 S-L3, A-F16), so a caller can report the key path.</summary>
[Trait("Lane", "unit")]
public sealed class ExtraPatternValidationTests
{
    private const string Good = "ACME-[0-9]{6}";

    public static TheoryData<string> MalformedIds() =>
    [
        string.Empty,
        "Acme-customer-id",
        "acme/customer-id",
        "acme customer",
        "acme_customer",
        "-acme",
        "acme-",
        "acme--id",
        "1acme",
        "acme]",
        "acme-customer-id\n",
        "a" + new string('b', 64),
    ];

    public static TheoryData<string> ReservedIds() => [.. Redactor.Default.RuleIds.Distinct(StringComparer.Ordinal), "blocked", "secret-field"];

    public static TheoryData<string> InvalidPatterns() =>
    [
        string.Empty,
        "ACME-[0-9",
        "(?<v>ACME",
        "ACME-[0-9]{6})",
        "[A-Z]*",
        "(?:ACME-[0-9]{6})?",
        "A" + new string('B', 1024),
    ];

    public static TheoryData<string, string, int> UnicodeShorthands() => new()
    {
        { "ACME-\\d{6}", "[0-9]", 5 },
        { "ACME-\\D", "[0-9]", 5 },
        { "\\w+-[0-9]{6}", "[A-Za-z0-9_]", 0 },
        { "ACME\\W", "[A-Za-z0-9_]", 4 },
        { "ACME\\s[0-9]{6}", "[ \\t\\r\\n]", 4 },
        { "ACME\\S", "[ \\t\\r\\n]", 4 },
        { "\\bACME-[0-9]{6}", "lookaround", 0 },
        { "ACME\\B", "lookaround", 4 },
        { "ACME-[\\d]{6}", "[0-9]", 6 },
        { "ACME-\\p{Nd}{6}", "[0-9] or [A-Za-z]", 5 },
        { "ACME-\\P{L}", "[0-9] or [A-Za-z]", 5 },
        { "C:\\\\\\d", "[0-9]", 4 },
    };

    [Theory]
    [MemberData(nameof(MalformedIds))]
    public void A_malformed_id_is_refused(string id) =>
        _ = AssertRefused(Redactor.Create([new(id, Good)], Redactor.DefaultMatchTimeout, new FakeTimeProvider()), 0, ExtraPatternField.Id, RedactionCodes.InvalidPatternId);

    [Theory]
    [MemberData(nameof(ReservedIds))]
    public void A_built_in_or_reserved_id_is_refused(string id)
    {
        var result = Redactor.Create([new(id, Good)], Redactor.DefaultMatchTimeout, new FakeTimeProvider());

        var error = AssertRefused(result, 0, ExtraPatternField.Id, RedactionCodes.InvalidPatternId);
        Assert.Contains(id, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_repeated_id_is_refused_at_its_second_entry()
    {
        var result = Redactor.Create([new("acme-id", Good), new("acme-id", "ACME-[A-Z]{3}")], Redactor.DefaultMatchTimeout, new FakeTimeProvider());

        _ = AssertRefused(result, 1, ExtraPatternField.Id, RedactionCodes.InvalidPatternId);
    }

    [Theory]
    [MemberData(nameof(InvalidPatterns))]
    public void An_empty_overlong_or_unparsable_pattern_is_refused(string pattern) =>
        _ = AssertRefused(Redactor.Create([new("acme-id", pattern)], Redactor.DefaultMatchTimeout, new FakeTimeProvider()), 0, ExtraPatternField.Pattern, RedactionCodes.InvalidPattern);

    [Theory]
    [MemberData(nameof(UnicodeShorthands))]
    public void A_unicode_aware_shorthand_is_refused_with_its_ascii_replacement(string pattern, string replacement, int offset)
    {
        var result = Redactor.Create([new("acme-id", pattern)], Redactor.DefaultMatchTimeout, new FakeTimeProvider());

        var error = AssertRefused(result, 0, ExtraPatternField.Pattern, RedactionCodes.InvalidPattern);
        Assert.Contains(replacement, error.Message, StringComparison.Ordinal);
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"offset {offset}"), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unparsable_pattern_is_refused_with_the_parse_error_and_its_offset()
    {
        var error = Assert.Single(Redactor.Create([new("acme-id", "ACME-[0-9")], Redactor.DefaultMatchTimeout, new FakeTimeProvider()).Errors);

        Assert.Contains("UnterminatedBracket at offset 9", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_lookaround_only_pattern_is_accepted_but_redacts_nothing()
    {
        var created = Redactor.Create([new("after-customer", "(?<=customer=)")], Redactor.DefaultMatchTimeout, new FakeTimeProvider());

        Assert.True(created.IsValid);
        Assert.Equal(ScrubOutcome.Clean, created.Redactor.Scrub("customer=123456").Outcome);
    }

    [Fact]
    public void An_id_and_a_pattern_at_their_length_limits_are_accepted()
    {
        var id = "a" + new string('b', 63);
        var pattern = "A" + new string('B', 1023);

        Assert.True(Redactor.Create([new(id, pattern)], Redactor.DefaultMatchTimeout, new FakeTimeProvider()).IsValid);
    }

    // why: the null-forgiving operators stand in for an empty list item or a missing field in configuration.
    [Fact]
    public void An_empty_entry_or_a_missing_field_is_refused_not_thrown()
    {
        ExtraPattern[] extras = [null!, new(null!, Good), new("acme-id", null!)];

        var result = Redactor.Create(extras, Redactor.DefaultMatchTimeout, new FakeTimeProvider());

        Assert.Equal(
            [(0, ExtraPatternField.Id), (1, ExtraPatternField.Id), (2, ExtraPatternField.Pattern)],
            result.Errors.Select(e => (e.Index, e.Field)));
    }

    [Theory]
    [InlineData("C:\\\\dir")]
    [InlineData("ACME-[0-9]{6}\\.\\-")]
    [InlineData("(?i)acme-[0-9]{6}")]
    [InlineData("(?<![A-Za-z0-9_])ACME-[0-9]{6}(?![A-Za-z0-9_])")]
    public void A_pattern_with_explicit_classes_and_escaped_backslashes_is_accepted(string pattern) =>
        Assert.True(Redactor.Create([new("acme-id", pattern)], Redactor.DefaultMatchTimeout, new FakeTimeProvider()).IsValid);

    [Fact]
    public void The_message_never_quotes_the_pattern()
    {
        var pattern = "SECRETISH-[0-9";

        var error = Assert.Single(Redactor.Create([new("acme-id", pattern)], Redactor.DefaultMatchTimeout, new FakeTimeProvider()).Errors);

        Assert.DoesNotContain("SECRETISH", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_refused_field_is_reported_in_entry_order()
    {
        ExtraPattern[] extras =
        [
            new("acme-id", Good),
            new("Bad/Id", "ACME-\\d"),
            new("other-id", "("),
        ];

        var result = Redactor.Create(extras, Redactor.DefaultMatchTimeout, new FakeTimeProvider());

        Assert.False(result.IsValid);
        Assert.Null(result.Redactor);
        Assert.Equal(
            [(1, ExtraPatternField.Id), (1, ExtraPatternField.Pattern), (2, ExtraPatternField.Pattern)],
            result.Errors.Select(e => (e.Index, e.Field)));
    }

    [Fact]
    public void A_timeout_past_the_regex_maximum_is_capped_not_thrown()
    {
        var result = Redactor.Create([new("acme-customer-id", Good)], TimeSpan.FromDays(30), new FakeTimeProvider());

        Assert.True(result.IsValid);
        Assert.Equal("order [REDACTED:acme-customer-id] shipped", result.Redactor.Scrub("order ACME-123456 shipped").Text);
        _ = AssertRefused(Redactor.Create([new("acme-id", "ACME-\\d")], TimeSpan.FromDays(30), new FakeTimeProvider()), 0, ExtraPatternField.Pattern, RedactionCodes.InvalidPattern);
    }

    private static ExtraPatternError AssertRefused(RedactorResult result, int index, ExtraPatternField field, string code)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Redactor);
        var error = Assert.Single(result.Errors);
        Assert.Equal((index, field, code), (error.Index, error.Field, error.Code));
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        return error;
    }
}
