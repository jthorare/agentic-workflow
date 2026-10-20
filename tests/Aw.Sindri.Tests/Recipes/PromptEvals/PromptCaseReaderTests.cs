using Aw.Sindri.Features.Recipes.PromptEvals;

namespace Aw.Sindri.Tests.Recipes.PromptEvals;

public sealed class PromptCaseReaderTests
{
    private const string Valid = """
        {
          "step": "draft", "goal": "Draft the scope.", "outputSchema": "{\"type\":\"object\"}", "seed": "You draft scopes.",
          "judges": [{ "id": "asks-answered", "criterion": "Asks nothing already answered.",
            "examples": [{ "output": "ok", "pass": true }], "heldOut": [{ "output": "bad", "pass": false }] }],
          "cases": [{ "id": "c1", "split": "heldOut", "input": "Add expiry.",
            "checks": [{ "check": "contains", "value": "expiry" }], "judges": ["asks-answered"] }]
        }
        """;

    [Fact, Trait("Lane", "unit")]
    public void A_valid_file_reads_with_its_judges_and_cases()
    {
        var read = PromptCaseReader.Read(Valid);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        var file = read.Value;
        Assert.Equal(("draft", "You draft scopes."), (file.Step, file.Seed));
        Assert.Equal("asks-answered", Assert.Single(file.Judges).Id);
        var c = Assert.Single(file.Cases);
        Assert.Equal(("c1", "heldOut"), (c.Id, c.Split));
        Assert.Equal(new PromptCheck("contains", "expiry"), Assert.Single(c.Checks));
        Assert.Equal(["asks-answered"], c.JudgeIds);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_case_with_no_split_is_tuning_and_one_with_no_graders_is_refused()
    {
        var tuning = PromptCaseReader.Read(Valid.Replace("\"split\": \"heldOut\", ", string.Empty, StringComparison.Ordinal));
        Assert.Equal("tune", tuning.Value.Cases[0].Split);

        var none = PromptCaseReader.Read(Valid.Replace("\"checks\": [{ \"check\": \"contains\", \"value\": \"expiry\" }], \"judges\": [\"asks-answered\"]", "\"checks\": []", StringComparison.Ordinal));
        Assert.Equal("SND-RECIPE-012", none.Error.Code);
        Assert.Contains("c1", none.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("not json", "JSON")]
    [InlineData("{}", "step")]
    [InlineData("""{"step":"draft","goal":"g","outputSchema":"s","seed":"x","judges":[],"cases":[]}""", "cases")]
    [InlineData("""{"step":"draft","goal":"g","outputSchema":"s","seed":"x","judges":[],"cases":[{"id":"a","input":"i","checks":[{"check":"vibes","value":"v"}]}]}""", "vibes")]
    [InlineData("""{"step":"draft","goal":"g","outputSchema":"s","seed":"x","judges":[],"cases":[{"id":"a","input":"i","checks":[],"judges":["ghost"]}]}""", "ghost")]
    [InlineData("""{"step":"draft","goal":"g","outputSchema":"s","seed":"x","judges":[],"cases":[{"id":"a","split":"later","input":"i","checks":[{"check":"isJson"}]}]}""", "later")]
    [InlineData("""{"step":"draft","goal":"g","outputSchema":"s","seed":"x","judges":[],"cases":[{"id":"a","input":"i","checks":[{"check":"matches","value":"("}]}]}""", "matches")]
    public void A_malformed_file_is_refused_naming_the_problem(string text, string mentions)
    {
        var read = PromptCaseReader.Read(text);

        Assert.Equal("SND-RECIPE-012", read.Error.Code);
        Assert.Contains(mentions, read.Error.Message, StringComparison.Ordinal);
    }
}
