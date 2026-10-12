using System.Text.Json.Nodes;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Redaction;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Aw.Redaction;
using static Aw.Judge.Tests.Evaluation.EvaluatorHarness;

namespace Aw.Judge.Tests.Evaluation;

[Lane(TestLane.Unit)]
public sealed class StoredInputTests
{
    private static async Task<string> Stored(JsonNode input)
    {
        var harness = new EvaluatorHarness(redactor: new AwInputRedactor(Redactor.Default));
        _ = await harness.Run(A.Question().Build(), input, new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1));
        return harness.Row.Details?.InputJson ?? "";
    }

    [Fact]
    public async Task A_secret_in_an_input_over_the_cap_is_never_stored_and_the_stored_input_stays_valid_json()
    {
        // Capping first would cut the key at the boundary, too short for any pattern; redacting first leaves nothing.
        var secrets = $"{SampleSecrets.AnthropicKey} {SampleSecrets.GitHubToken} {SampleSecrets.FortyHex}";
        var text = $"{new string('z', 16_000 - 9 - 1 - 10)} {secrets} {new string('y', 100)}";

        var stored = await Stored(Text(text));

        Assert.True(stored.Length <= InputCap.MaxChars, $"{stored.Length} chars");
        Assert.DoesNotContain(SampleSecrets.AnthropicKey[..10], stored, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.GitHubToken[..10], stored, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.FortyHex[..20], stored, StringComparison.Ordinal);
        Assert.Matches(@"\[truncated \d+ chars\]$", JsonNode.Parse(stored)?["text"]?.GetValue<string>());
    }

    [Fact]
    public async Task An_oversize_invalid_input_is_stored_as_valid_json_with_a_marker_and_the_digest_unchanged()
    {
        var input = new JsonObject { ["brief"] = "b", ["diff"] = string.Concat(Enumerable.Repeat("+line\n", 10_000)) };
        var harness = new EvaluatorHarness();

        _ = await harness.Run(A.Question().Build(), input, new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1));

        var stored = JsonNode.Parse(harness.Row.Details?.InputJson ?? "");
        Assert.Equal("b", stored?["brief"]?.GetValue<string>());
        Assert.Matches(@"\[truncated \d+ chars\]$", stored?["diff"]?.GetValue<string>());
        Assert.Equal(InputJson.Digest(input, JsonOptions), harness.Row.InputDigest);
    }

    [Fact]
    public async Task An_escalation_after_the_input_validated_stores_only_the_declared_fields()
    {
        // Nothing decides, so the call escalates; the input had validated, so the row holds the record, not the caller's text.
        var harness = new EvaluatorHarness();
        var input = new JsonObject { ["text"] = "hmm", ["note"] = "not declared" };

        var outcome = await harness.Run(A.Question().Build(), input);

        Assert.Equal(new EscalatedOutcome("no-provider-decided"), outcome);
        Assert.Equal("""{"text":"hmm"}""", harness.Row.Details?.InputJson);
    }

    [Fact]
    public void An_input_that_fits_is_written_unchanged() => Assert.Equal("""{"a":1}""", InputCap.Write(new JsonObject { ["a"] = 1 }, 100, JsonOptions));

    [Fact]
    public void Over_the_cap_long_strings_are_cut_with_a_marker_and_short_ones_kept()
    {
        var value = new JsonObject
        {
            ["a"] = new string('a', 1000),
            ["list"] = new JsonArray(new JsonObject { ["b"] = "short" }, new JsonObject { ["c"] = new string('c', 5000) }),
            ["n"] = 1,
        };

        var text = InputCap.Write(value, 4000, JsonOptions);

        Assert.True(text.Length <= 4000, $"{text.Length} chars");
        var parsed = JsonNode.Parse(text)!;
        Assert.Equal(1, parsed["n"]!.GetValue<int>());
        Assert.Equal("short", parsed["list"]![0]!["b"]!.GetValue<string>());
        Assert.Matches(@"\[truncated \d+ chars\]$", parsed["list"]![1]!["c"]!.GetValue<string>());
        Assert.Equal(1000, parsed["a"]!.GetValue<string>().Length);
        Assert.Equal(5000, value["list"]![1]!["c"]!.GetValue<string>().Length);
    }

    [Fact]
    public void Input_still_over_the_cap_after_cutting_strings_is_stored_as_a_valid_stub()
    {
        var numbers = new JsonObject { ["nums"] = new JsonArray([.. Enumerable.Range(0, 20_000).Select(i => (JsonNode)i)]) };

        var text = InputCap.Write(numbers, InputCap.MaxChars, JsonOptions);

        Assert.True(text.Length <= InputCap.MaxChars, $"{text.Length} chars");
        Assert.NotNull(JsonNode.Parse(text)?["truncated"]);
    }

    [Fact]
    public void Truncate_keeps_short_text_and_marks_cut_text()
    {
        Assert.Equal("abc", InputCap.Truncate("abc", 3));
        Assert.Equal("abcd[truncated 2 chars]", InputCap.Truncate("abcdef", 4));
    }

    [Fact]
    public void The_digest_is_the_first_16_hex_of_the_sha256_of_the_compact_json()
    {
        var digest = InputJson.Digest(new JsonObject { ["text"] = "hi" }, JsonOptions);

        Assert.Equal(16, digest.Length);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("""{"text":"hi"}"""u8))[..16], digest);
    }
}
