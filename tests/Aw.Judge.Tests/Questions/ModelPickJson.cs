using System.Text.Json.Nodes;

namespace Aw.Judge.Tests.Questions;

/// <summary>model-pick inputs for the tests: three candidates (haiku, sonnet, opus, cheapest first) and the files the test names.</summary>
internal static class ModelPickJson
{
    public const string Brief = "Write the interview questions for the item.";

    public static string Input(
        IEnumerable<(string Path, string Text)>? files = null,
        string brief = Brief,
        int candidates = 3,
        IEnumerable<string>? denyPaths = null,
        string kind = "questions")
    {
        var root = new JsonObject
        {
            ["step"] = "step.interview.questions",
            ["kind"] = kind,
            ["brief"] = brief,
            ["files"] = new JsonArray([.. (files ?? []).Select(f => (JsonNode)new JsonObject { ["path"] = f.Path, ["text"] = f.Text })]),
            ["candidates"] = Candidates(candidates),
        };
        if (denyPaths is not null)
        {
            root["denyPaths"] = new JsonArray([.. denyPaths.Select(p => (JsonNode)JsonValue.Create(p))]);
        }

        return root.ToJsonString();
    }

    public static JsonArray Candidates(int count)
    {
        string[] models = ["haiku", "sonnet", "opus"];
        string[] tiers = ["cheap", "standard", "strong"];
        return new JsonArray([.. Enumerable.Range(0, count).Select(i => (JsonNode)new JsonObject
        {
            ["key"] = "m" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["model"] = i < models.Length ? models[i] : "model-" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["tier"] = i < tiers.Length ? tiers[i] : "strong",
        })]);
    }

    /// <summary>The input with one top-level member replaced (or removed with null).</summary>
    public static string With(string input, string member, JsonNode? value)
    {
        var root = JsonNode.Parse(input)!.AsObject();   // why: the helper's own input is an object
        root[member] = value;
        return root.ToJsonString();
    }

    /// <summary>Source lines of 100 characters each, so a cut at a line lands under the cap.</summary>
    public static string Lines(int totalChars) =>
        string.Concat(Enumerable.Repeat(new string('x', 99) + "\n", totalChars / 100));
}
