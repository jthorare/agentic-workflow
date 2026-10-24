using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The interview's "fact:answers": an append-only array of batches. A round adds one batch to the array as it stands and the whole array is written back;
/// an earlier batch is never edited.
/// </summary>
internal static class ScopingAnswers
{
    /// <summary>The array with one more batch at its end.</summary>
    /// <param name="existing">The array as it stands (empty array text when nothing was answered yet).</param>
    /// <param name="shown">The ids of the questions this round showed, in order.</param>
    /// <param name="chosen">The answers of the questions that have one, in the order shown.</param>
    /// <param name="at">When the round was sent.</param>
    /// <returns>The JSON text of the whole array; null when <paramref name="existing"/> is not an array.</returns>
    internal static string? Append(string existing, IReadOnlyList<string> shown, IReadOnlyList<JsonObject> chosen, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(chosen);
        if (JsonNode.Parse(existing) is not JsonArray batches)
        {
            return null;
        }

        var ids = new JsonArray();
        foreach (var id in shown)
        {
            ids.Add((JsonNode?)JsonValue.Create(id));
        }

        var answers = new JsonArray();
        foreach (var answer in chosen)
        {
            answers.Add((JsonNode?)answer);
        }

        JsonNode batch = new JsonObject
        {
            ["round"] = batches.Count + 1,
            ["actor"] = "owner",
            ["at"] = at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture),
            ["questionIds"] = ids,
            ["answers"] = answers,
        };
        batches.Add(batch);
        return batches.ToJsonString();
    }
}
