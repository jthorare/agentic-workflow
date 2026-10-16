using Aw.Evidence.Core.Data;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Data;

public sealed class RecordedTextTests
{
    private static FakeTextScrubber Scrubber() => new FakeTextScrubber().HidesName("password").HidesName("password_hash").HidesName("api_tokens");

    [Fact, Trait("Lane", "unit")]
    public void A_json_object_with_a_duplicate_key_keeps_its_ordinary_members_and_hides_the_secret_one()
    {
        var recorded = RecordedText.Of(Scrubber(), """[{"id":1,"id":2,"password":"pw-1234567"}]""", RecordedText.OutputLimit);

        Assert.Contains("\"id\":2", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain("pw-1234567", recorded, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_body_with_a_repeated_key_hides_every_secret_named_member_wherever_it_is()
    {
        const string Body = """{"a":1,"a":2,"password_hash":9876543,"api_tokens":["tok-AAA111","tok-BBB222"],"nested":{"x":1,"x":2,"password":"pw-PLANTED-1"},"list":[{"password_hash":"hash-PLANTED-2","k":1,"k":2}],"password":"pw-PLANTED-3"}""";

        var recorded = RecordedText.Of(Scrubber(), Body, RecordedText.OutputLimit);

        foreach (var value in new[] { "9876543", "tok-AAA111", "tok-BBB222", "pw-PLANTED-1", "hash-PLANTED-2", "pw-PLANTED-3" })
        {
            Assert.DoesNotContain(value, recorded, StringComparison.Ordinal);
        }

        Assert.Contains("\"a\":2", recorded, StringComparison.Ordinal);
        Assert.Contains("\"x\":2", recorded, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_that_is_not_json_is_still_scrubbed_by_value()
    {
        var recorded = RecordedText.Of(new FakeTextScrubber().Hides("pw-1234567"), "{not json pw-1234567", RecordedText.OutputLimit);

        Assert.DoesNotContain("pw-1234567", recorded, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Hiding_replaces_exactly_the_secret_values_and_keeps_the_rest_as_written()
    {
        var recorded = RecordedText.Of(Scrubber(), """[{"id": 1, "password": "pw-1", "o": {"x":2}}]""", RecordedText.OutputLimit);

        Assert.Equal("""[{"id": 1, "password": "[REDACTED:secret-field]", "o": {"x":2}}]""", recorded);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_secret_member_written_with_an_escape_in_its_key_is_hidden()
    {
        var recorded = RecordedText.Of(Scrubber(), """{"passw\u006frd":"pw-ESC-1","ok":1}""", RecordedText.OutputLimit);

        Assert.Equal("""{"passw\u006frd":"[REDACTED:secret-field]","ok":1}""", recorded);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_object_valued_secret_member_is_hidden_whole()
    {
        var recorded = RecordedText.Of(Scrubber(), """{"password":{"a":"pw-OBJ-1","b":[1,2]},"ok":1}""", RecordedText.OutputLimit);

        Assert.Equal("""{"password":"[REDACTED:secret-field]","ok":1}""", recorded);
    }

    [Fact, Trait("Lane", "unit")]
    public void Members_are_hidden_before_the_value_scrub_so_a_numeric_secret_cannot_break_the_json_first()
    {
        var recorded = RecordedText.Of(new FakeTextScrubber().Hides("123456").HidesName("password"), """[{"pin":123456,"password":"abc"}]""", RecordedText.OutputLimit);

        Assert.DoesNotContain("abc", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", recorded, StringComparison.Ordinal);
    }
}
