using System.Text.Json.Nodes;
using Aw.Judge.Infrastructure.Redaction;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Aw.Redaction;

namespace Aw.Judge.Tests.Redaction;

[Lane(TestLane.Unit)]
public sealed class AwInputRedactorTests
{
    private static string Redact(string text) =>
        new AwInputRedactor(Redactor.Default).Redact(JsonValue.Create(text))?.GetValue<string>() ?? "";

    public static TheoryData<string> JudgeSecrets() =>
        [SampleSecrets.AnthropicKey, SampleSecrets.GitHubToken, SampleSecrets.AwsKeyId, SampleSecrets.SlackToken, SampleSecrets.Jwt, SampleSecrets.FortyHex];

    [Theory]
    [MemberData(nameof(JudgeSecrets))]
    public void Every_judge_pattern_still_holds(string secret)
    {
        var redacted = Redact($"key={secret} rest");

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.EndsWith(" rest", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_standalone_40_hex_run_is_redacted_though_the_shared_rules_leave_it()
    {
        Assert.Equal(SampleSecrets.FortyHex, Redactor.Default.Scrub(SampleSecrets.FortyHex).Text);

        Assert.Equal($"sha {AwInputRedactor.Placeholder}", Redact($"sha {SampleSecrets.FortyHex}"));
    }

    [Fact]
    public void Short_shas_and_hex_inside_words_survive()
    {
        Assert.Equal("fix in 855d6f8, see chain.ts", Redact("fix in 855d6f8, see chain.ts"));
        Assert.Equal($"x{SampleSecrets.FortyHex}", Redact($"x{SampleSecrets.FortyHex}"));
    }
}
