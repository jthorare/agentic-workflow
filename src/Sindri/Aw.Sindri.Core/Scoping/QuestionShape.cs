using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Scoping;

/// <summary>What 'fact:questions' needs beyond its schema (SCP-QST-001, #1204): every question has two to four options and exactly one of them recommended, like a design decision (DSN-DEC-001). Read by the scoping rule and when the questions agent reports.</summary>
public static class QuestionShape
{
    /// <summary>What is wrong with each question of <paramref name="questions"/>, with its id.</summary>
    /// <param name="questions">The parsed fact:questions, or null.</param>
    /// <returns>One entry per fault: question id, what is wrong, what to change.</returns>
    public static IReadOnlyList<(string Id, string Message, string Fix)> Faults(JsonNode? questions)
    {
        var faults = new List<(string Id, string Message, string Fix)>();
        if (questions is not JsonArray all)
        {
            return faults;
        }

        foreach (var question in all.OfType<JsonObject>())
        {
            var id = question["id"] is JsonValue v && v.TryGetValue<string>(out var text) ? text : "?";
            var options = question["options"] is JsonArray list ? list.OfType<JsonObject>().ToList() : [];
            if (options.Count is < 2 or > 4)
            {
                faults.Add((id, $"{id} has {options.Count.ToString(CultureInfo.InvariantCulture)} options", "Give it two to four options."));
            }

            var recommended = options.Count(static o => o["recommended"] is JsonValue r && r.TryGetValue<bool>(out var yes) && yes);
            if (recommended != 1)
            {
                faults.Add((id, $"{id} has {recommended.ToString(CultureInfo.InvariantCulture)} recommended options", "Mark exactly one option recommended; for a question that can't be decided yet, recommend the option that defers and reports it, or move it to openQuestions."));
            }
        }

        return faults;
    }
}
