namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class SecretShapeTests
{
    [Theory]
    [MemberData(nameof(Corpus.Plain), MemberType = typeof(Corpus), DisableDiscoveryEnumeration = true)]
    [MemberData(nameof(Corpus.LongAndShellShapes), MemberType = typeof(Corpus), DisableDiscoveryEnumeration = true)]
    public void Each_secret_shape_is_replaced_by_its_rule_placeholder(SecretCase secret)
    {
        var result = Redactor.Default.Scrub(secret.Input);

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.Contains($"[REDACTED:{secret.Rule}]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(secret.Fragment, result.Text, StringComparison.Ordinal);
        Assert.Contains(result.Hits, h => h.Rule == secret.Rule);
        Assert.Null(result.BlockCode);
    }

    [Theory]
    [MemberData(nameof(Corpus.Evasions), MemberType = typeof(Corpus), DisableDiscoveryEnumeration = true)]
    public void Evasion_forms_are_still_caught(SecretCase secret)
    {
        var result = Redactor.Default.Scrub(secret.Input);

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.Contains($"[REDACTED:{secret.Rule}]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(secret.Fragment, result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_rule_keeps_the_name_and_redacts_only_the_value()
    {
        var input = Shapes.Join("Authorization: Bear", "er ", Shapes.Body, Shapes.Body);

        var result = Redactor.Default.Scrub(input);

        Assert.Equal("Authorization: Bearer [REDACTED:bearer]", result.Text);
        var hit = Assert.Single(result.Hits);
        Assert.Equal(new RedactionHit("bearer", input.IndexOf(Shapes.Body, StringComparison.Ordinal), input.Length), hit);
    }

    [Fact]
    public void A_zero_width_joiner_inside_a_token_is_redacted_with_the_token()
    {
        var input = Shapes.Join("x gh", "p_", "Ab3dE6gH9jK2mN5p", "\u200D", Shapes.Body, " y");

        var result = Redactor.Default.Scrub(input);

        Assert.Equal("x [REDACTED:github-token] y", result.Text);
    }

    [Fact]
    public void An_unterminated_key_block_is_redacted_to_the_end_of_its_body()
    {
        var input = Shapes.Pem(string.Empty) + "\nafter the key";

        var result = Redactor.Default.Scrub(input);

        Assert.StartsWith("[REDACTED:private-key]", result.Text, StringComparison.Ordinal);
        Assert.EndsWith("after the key", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Shapes.Tail, result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlapping_rules_produce_one_placeholder_named_by_the_earlier_rule()
    {
        var input = Shapes.Join("Bear", "er ", "sk-", "ant-", Shapes.Body, Shapes.Body);

        var result = Redactor.Default.Scrub(input);

        var hit = Assert.Single(result.Hits);
        Assert.Equal("anthropic-key", hit.Rule);
        Assert.Equal("Bearer [REDACTED:anthropic-key]", result.Text);
    }

    [Fact]
    public void Many_secrets_in_one_text_are_each_redacted()
    {
        var key = Shapes.Join("gh", "p_", Shapes.Body, "Ab3dE6gH9");
        var input = string.Join(' ', Enumerable.Repeat(key, 200));

        var result = Redactor.Default.Scrub(input);

        Assert.Equal(200, result.Hits.Count);
        Assert.DoesNotContain(Shapes.Tail, result.Text, StringComparison.Ordinal);
    }
}
