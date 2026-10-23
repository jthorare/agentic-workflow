using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// The one way a lens loop reaches a human (owner, 2026-10-19): a stopping finding carrying a typed
/// <c language="text">escalate: { kind: "ambiguousFix" | "productQuestion", question, options? }</c> (schemas/line/escalation.json). Anything else, a finding that only says
/// "please decide", an unknown kind, a missing question or an option that is not text, is not an escalation and stays in the loop.
/// </summary>
internal static class Escalation
{
    private static readonly string[] _kinds = ["ambiguousFix", "productQuestion"];

    /// <summary>The first typed escalation among the findings, as 'fact:escalation': the kind, the question, the options when there are any and the finding's lens and id.</summary>
    /// <param name="stopping">The round's findings that stop the loop, each with its lens.</param>
    /// <returns>The escalation; null when no finding carries a typed one.</returns>
    internal static JsonObject? First(IEnumerable<JsonObject> stopping)
    {
        ArgumentNullException.ThrowIfNull(stopping);
        return stopping.Select(Of).OfType<JsonObject>().FirstOrDefault();
    }

    /// <summary>
    /// Every typed escalation among the findings, in order and with no limit (owner, 2026-10-23): the design review asks all of a round's questions at once, in one desk.
    /// Each carries a stable 'id', <c language="text">q1</c>, <c language="text">q2</c>... in round order, so two questions with the same text from different lenses stay apart and an answer can name its question.
    /// </summary>
    /// <param name="stopping">The round's findings that stop the loop, each with its lens.</param>
    /// <returns>One entry per escalating finding, as <see cref="First"/> writes it plus its id; empty when no finding carries a typed one.</returns>
    internal static JsonArray All(IEnumerable<JsonObject> stopping)
    {
        ArgumentNullException.ThrowIfNull(stopping);
        var all = new JsonArray();
        foreach (var escalation in stopping.Select(Of).OfType<JsonObject>())
        {
            escalation["id"] = string.Create(CultureInfo.InvariantCulture, $"q{all.Count + 1}");
            all.Add((JsonNode?)escalation);
        }

        return all;
    }

    private static JsonObject? Of(JsonObject finding)
    {
        if (Typed(finding["escalate"]) is not { } typed)
        {
            return null;
        }

        typed["lens"] = LoopJson.Str(finding, "lens");
        if (LoopJson.Str(finding, "id") is { Length: > 0 } id)
        {
            typed["finding"] = id;
        }

        return typed;
    }

    private static JsonObject? Typed(JsonNode? node)
    {
        if (node is not JsonObject escalate
            || LoopJson.Str(escalate, "kind") is not { } kind || !_kinds.Contains(kind, StringComparer.Ordinal)
            || LoopJson.Str(escalate, "question") is not { Length: > 0 } question)
        {
            return null;
        }

        var typed = new JsonObject { ["kind"] = kind, ["question"] = question };
        if (escalate["options"] is null)
        {
            return typed;
        }

        if (escalate["options"] is not JsonArray { Count: > 0 } options || options.Any(static o => o is not JsonValue v || !v.TryGetValue<string>(out _)))
        {
            return null;
        }

        typed["options"] = options.DeepClone();
        return typed;
    }
}
