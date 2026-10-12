using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The recipe hash (spec §3.1): sha256 over RFC 8785 of the long form without 'hash'.</summary>
public sealed class RecipeHashingTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"b\":1,\"a\":[{\"d\":null,\"c\":\"é\"}]}", "{\"a\":[{\"c\":\"é\",\"d\":null}],\"b\":1}")]
    [InlineData("[1.0, 4.50, 1E30, 2e-3, 0.000001, 1e-7, 123456789012345678901234, -0.0, 100]", "[1,4.5,1e+30,0.002,0.000001,1e-7,1.2345678901234569e+23,0,100]")]
    [InlineData("{\"\\u20ac\":1,\"$\":2,\"\\u000f\":3,\"\\n\":4,\"A\":5}", "{\"\\n\":4,\"\\u000f\":3,\"$\":2,\"A\":5,\"€\":1}")]
    public void Canonical_json_follows_rfc_8785(string input, string expected) =>
        Assert.Equal(expected, Encoding.UTF8.GetString(CanonicalJson.Of(JsonNode.Parse(input))));

    /// <summary>RFC 8785 §3.2.4's example, checked independently with Python's json module (sort_keys, no spaces, no ASCII escaping).</summary>
    [Fact, Trait("Lane", "unit")]
    public void Canonical_json_matches_the_rfc_example()
    {
        const string Input = "{ \"numbers\": [333333333.33333329, 1E30, 4.50, 2e-3, 0.000000000000000000000000001], \"string\": \"\\u20ac$\\u000F\\u000aA'\\u0042\\u0022\\u005c\\\\\\\"\\/\", \"literals\": [null, true, false] }";

        Assert.Equal(
            "{\"literals\":[null,true,false],\"numbers\":[333333333.3333333,1e+30,4.5,0.002,1e-27],\"string\":\"€$\\u000f\\nA'B\\\"\\\\\\\\\\\"/\"}",
            Encoding.UTF8.GetString(CanonicalJson.Of(JsonNode.Parse(Input))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_short_form_and_its_long_form_hash_the_same()
    {
        var shortForm = RecipeReaderTests.Reader.Read(RecipeFixtures.Read("valid/short-forms.yaml"), "short-forms.yaml").Document!;
        var longForm = RecipeReaderTests.Reader.Read(RecipeFixtures.Read("valid/long-forms.yaml"), "long-forms.yaml").Document!;

        Assert.Equal(AwJson.Serialize(longForm.Recipe, SliceHarness.Json), AwJson.Serialize(shortForm.Recipe, SliceHarness.Json));
        Assert.Equal(longForm.Hash, shortForm.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_stated_hash_and_formatting_dont_change_the_hash()
    {
        var plain = RecipeReaderTests.Reader.Read(RecipeReaderTests.Minimal, "a.yaml").Document!.Hash;
        var restyled = RecipeReaderTests.Reader.Read(
            "# a comment\n" + RecipeReaderTests.Minimal.Replace("next: \"Produce the Step output.\"", "next: 'Produce the Step output.'", StringComparison.Ordinal)
                + "\nhash: sha256:" + new string('0', 64) + "\n",
            "b.yaml").Document!.Hash;

        Assert.Equal(plain, restyled);
    }

    /// <summary>
    /// A known vector: the minimal recipe's canonical long form and its hash, checked independently with
    /// python3 -c 'import hashlib,json,sys; s=sys.stdin.read(); assert s==json.dumps(json.loads(s),sort_keys=True,separators=(",",":"),ensure_ascii=False); print(hashlib.sha256(s.encode()).hexdigest())'.
    /// </summary>
    [Fact, Trait("Lane", "unit")]
    public void The_minimal_recipe_hashes_to_a_known_vector()
    {
        var document = RecipeReaderTests.Reader.Read(RecipeReaderTests.Minimal, "test.yaml").Document!;
        var node = JsonNode.Parse(AwJson.Serialize(document.Recipe, SliceHarness.Json))!.AsObject();
        _ = node.Remove("hash");

        Assert.Equal(KnownCanonical, Encoding.UTF8.GetString(CanonicalJson.Of(node)));
        Assert.Equal(KnownHash, document.Hash.Value);
    }

    private const string KnownCanonical =
        "{\"base\":null,\"budget\":null,\"description\":null,\"evals\":[],\"id\":\"workflow.minimal\",\"initial\":\"produce\",\"inputs\":[],\"loops\":{},\"schema\":1,\"states\":{\"done\":{\"budget\":null,\"kind\":\"terminal\",\"loop\":null,\"next\":null,\"outcome\":\"resolved\",\"step\":null,\"timeout\":null,\"waitFor\":null},\"produce\":{\"budget\":null,\"kind\":\"active\",\"loop\":null,\"next\":\"Produce the Step output.\",\"outcome\":null,\"step\":{\"inputs\":[],\"use\":\"step.produce\",\"with\":{}},\"timeout\":{\"after\":\"4h\",\"then\":\"park\"},\"waitFor\":null}},\"transitions\":[{\"effects\":[],\"evidence\":[],\"from\":[\"produce\"],\"gates\":[],\"on\":\"step.completed\",\"produces\":[\"fact:output\"],\"to\":\"done\",\"when\":{\"all\":null,\"any\":null,\"below\":null,\"fact\":null,\"is\":null,\"loop\":null,\"not\":null,\"outcome\":\"done\"}}],\"version\":1}";

    private const string KnownHash = "sha256:7a3ab0cb4b52e09a8b90a968c7ce8cb5cf5f517d224e27a85a719335f9bb20c4";
}
