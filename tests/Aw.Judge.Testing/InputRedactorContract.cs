using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The IInputRedactor contract: the fake and the Aw.Redaction adapter both pass it.</summary>
public abstract class InputRedactorContract
{
    private static readonly IReadOnlyList<string> _secrets = SampleSecrets.Contract;

    /// <summary>A fresh redactor.</summary>
    /// <returns>The redactor.</returns>
    protected abstract IInputRedactor Create();

    /// <summary>A secret in a string value is replaced and the text around it kept.</summary>
    [Fact]
    public void A_secret_in_a_value_is_replaced_and_the_rest_kept()
    {
        var redacted = Create().Redact(new JsonObject { ["text"] = $"token {_secrets[0]} here" });

        var text = redacted?["text"]?.GetValue<string>() ?? "";
        Expect.That(!text.Contains(_secrets[0], StringComparison.Ordinal), "the secret is gone");
        Expect.That(text.StartsWith("token ", StringComparison.Ordinal) && text.EndsWith(" here", StringComparison.Ordinal), "the text around it stays");
    }

    /// <summary>A secret used as an object key is replaced and its value kept.</summary>
    [Fact]
    public void A_secret_in_a_key_is_replaced()
    {
        var redacted = Create().Redact(new JsonObject { [_secrets[1]] = "value" });

        Expect.That(redacted is JsonObject { Count: 1 } obj && !obj.ContainsKey(_secrets[1]), "the key no longer holds the secret");
        Expect.That(redacted?.AsObject().Single().Value?.GetValue<string>() == "value", "its value stays");
    }

    /// <summary>Nested strings are redacted; numbers, booleans, nulls and the shape are kept.</summary>
    [Fact]
    public void Nested_strings_are_redacted_and_other_values_kept()
    {
        var input = new JsonObject
        {
            ["a"] = new JsonArray($"x {_secrets[2]}", 1.5, null, true),
            ["b"] = new JsonObject { ["c"] = _secrets[0], ["n"] = 7 },
        };

        var json = Create().Redact(input)?.ToJsonString() ?? "";

        Expect.That(_secrets.All(s => !json.Contains(s, StringComparison.Ordinal)), "no secret survives anywhere in the tree");
        var output = JsonNode.Parse(json)!.AsObject();
        Expect.That(output["a"]!.ToJsonString().EndsWith(",1.5,null,true]", StringComparison.Ordinal), "the array keeps its numbers, null and booleans");
        Expect.That(output["b"]!["n"]!.ToJsonString() == "7", "nested numbers stay");
    }

    /// <summary>
    /// A field whose name says it holds a secret (client_secret, apiKey, password) has its whole value redacted, whatever
    /// the value looks like; the field itself and the fields around it stay.
    /// </summary>
    /// <param name="name">The field name.</param>
    [Theory]
    [InlineData("client_secret")]
    [InlineData("apiKey")]
    [InlineData("password")]
    public void A_secret_named_field_is_redacted_whatever_its_value(string name)
    {
        const string Shapeless = "correct horse battery staple";
        var input = new JsonObject { ["text"] = "kept", ["nested"] = new JsonObject { [name] = Shapeless, ["n"] = 2 } };

        var json = Create().Redact(input)?.ToJsonString() ?? "";

        Expect.That(!json.Contains("horse", StringComparison.Ordinal), $"the value of '{name}' is gone, got {json}");
        var output = JsonNode.Parse(json)!.AsObject();
        var nested = output["nested"]!.AsObject();
        Expect.That(nested.ContainsKey(name), "the field itself stays");
        Expect.That(output["text"]!.GetValue<string>() == "kept" && nested["n"]!.ToJsonString() == "2", "the other fields stay");
    }

    /// <summary>A secret right after a newline or a tab inside a value is still found.</summary>
    /// <param name="whitespace">The character before the secret.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\t")]
    public void A_secret_after_escaped_whitespace_is_replaced(string whitespace)
    {
        var redacted = Create().Redact(JsonValue.Create($"KEY={whitespace}{_secrets[0]}{whitespace}{_secrets[2]}"));

        var text = redacted?.GetValue<string>() ?? "";
        Expect.That(!text.Contains(_secrets[0], StringComparison.Ordinal) && !text.Contains(_secrets[2], StringComparison.Ordinal), "both secrets are gone");
        Expect.That(text.StartsWith($"KEY={whitespace}", StringComparison.Ordinal), "the text before them stays");
    }

    /// <summary>Text without secrets, JSON null and the input itself are left alone.</summary>
    [Fact]
    public void Clean_text_null_and_the_input_are_untouched()
    {
        var redactor = Create();
        var input = new JsonObject { ["text"] = "fix in 855d6f8, see chain.ts", ["other"] = _secrets[0] };
        var before = input.ToJsonString();

        var redacted = redactor.Redact(input);

        Expect.That(redacted?["text"]?.GetValue<string>() == "fix in 855d6f8, see chain.ts", "clean text is unchanged");
        Expect.That(input.ToJsonString() == before, "the input is not changed");
        Expect.That(redactor.Redact(input: null) is null, "JSON null stays null");
    }
}
