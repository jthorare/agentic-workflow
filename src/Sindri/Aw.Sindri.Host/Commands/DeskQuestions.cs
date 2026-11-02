using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// What an interview desk asks, for 'workflow status': the questions of 'fact:questions' with their option ids, the recommended option and the other and skip
/// choices, read through the facts route, and the shape 'fact:answers' takes. Only the desk of 'step.interview.answer' shows them (an earlier round's questions
/// stay on the instance after it moves on). The text of a question and of an option is model-written, so a session sees it inside the pack fence.
/// </summary>
internal static class DeskQuestions
{
    private const string AnswerStep = "step.interview.answer";
    private const string Key = "fact:questions";
    private const string Source = "scoping";

    /// <summary>The 'complete' hint for the answers: the batch the Step takes and the entry forms.</summary>
    internal const string AnswerHint =
        "Answer with --output fact:answers=<file>: the file is fact:answers as it stands (GET the fact) with this round's batch added at the end:\n"
        + "  {\"round\":<n>,\"actor\":\"owner\",\"at\":\"<UTC time>\",\"questionIds\":[\"Q1\",...],\"answers\":[<one entry per answered question>]}\n"
        + "  an entry: {\"question\":\"Q1\",\"option\":\"Q1.a\"} to pick an option, {\"question\":\"Q1\",\"other\":\"your own answer\"} or {\"question\":\"Q1\",\"skip\":true}\n"
        + "  --outcome answered when more may follow, --outcome doneAsking to stop asking.";

    /// <summary>The questions the desk of <paramref name="detail"/> asks; null when it asks none (no interview desk, or nothing produced yet).</summary>
    /// <param name="desk">The API client.</param>
    /// <param name="id">The instance.</param>
    /// <param name="detail">The instance's detail.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The questions as served, null for none, or the error that stopped the read.</returns>
    internal static async Task<Result<JsonElement?>> Read(RecipeDesk desk, WorkflowInstanceId id, InstanceDetail detail, CancellationToken ct)
    {
        if (detail.Current is not { Desk: not null, Step.Ref: AnswerStep })
        {
            return (JsonElement?)null;
        }

        var read = await desk.Client.Get<FactView>($"{InstanceReads.Instances}/{InstanceRoute.Segment(id.Value)}/facts/{Uri.EscapeDataString(Key)}", ct).ConfigureAwait(false);
        if (read.IsError)
        {
            if (string.Equals(read.Error.Code, SindriErrors.ItemNotFound.Code, StringComparison.Ordinal))
            {
                return (JsonElement?)null;   // not produced yet
            }

            return read.Error;
        }

        var body = read.Value.Value.Body;
        return body.ValueKind == JsonValueKind.Array ? (JsonElement?)body : null;
    }

    /// <summary>The questions as the JSON status carries them: id, question, dimension, options (id, answer, recommended), other and skip.</summary>
    /// <param name="body">The 'fact:questions' array.</param>
    /// <param name="fenced">Whether a session reads it: the model-written text goes inside the pack fence.</param>
    /// <returns>The array.</returns>
    internal static JsonArray Questions(JsonElement body, bool fenced) =>
        [.. body.EnumerateArray().Where(static q => q.ValueKind == JsonValueKind.Object && Str(q, "id") is { Length: > 0 }).Select(q => Question(q, fenced))];

    /// <summary>The status text with the questions after it and the answer hint.</summary>
    /// <param name="statusText">The §6.4 layout.</param>
    /// <param name="questions">The array <see cref="Questions"/> built.</param>
    /// <returns>The text.</returns>
    internal static string Render(string statusText, JsonArray questions)
    {
        var text = new StringBuilder(statusText);
        if (!statusText.EndsWith('\n'))
        {
            _ = text.Append('\n');
        }

        _ = text.Append("\nThe questions:\n");
        foreach (var question in questions.OfType<JsonObject>())
        {
            _ = text.Append("  ").Append(Str(question, "id")).Append(": ").Append(Str(question, "question")).Append('\n');
            foreach (var option in (question["options"] as JsonArray ?? []).OfType<JsonObject>())
            {
                _ = text.Append("    ").Append(Str(option, "id")).Append("  ").Append(Str(option, "answer")).Append(option["recommended"]?.GetValue<bool>() == true ? "  (recommended)" : string.Empty).Append('\n');
            }

            _ = text.Append("    ").Append(Choices(question)).Append('\n');
        }

        return text.Append('\n').Append(AnswerHint).Append('\n').ToString();
    }

    private static JsonObject Question(JsonElement q, bool fenced)
    {
        var options = new JsonArray();
        if (q.TryGetProperty("options", out var served) && served.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in served.EnumerateArray().Where(static o => o.ValueKind == JsonValueKind.Object && Str(o, "id") is { Length: > 0 }))
            {
                options.Add((JsonNode)new JsonObject
                {
                    ["id"] = Str(option, "id"),
                    ["answer"] = Untrusted(Str(option, "answer"), fenced),
                    ["recommended"] = option.TryGetProperty("recommended", out var flag) && flag.ValueKind == JsonValueKind.True,
                });
            }
        }

        var result = new JsonObject { ["id"] = Str(q, "id"), ["question"] = Untrusted(Str(q, "question"), fenced) };
        if (Str(q, "dimension") is { Length: > 0 } dimension)
        {
            result["dimension"] = dimension;
        }

        result["options"] = options;
        result["other"] = q.TryGetProperty("other", out var other) && other.ValueKind == JsonValueKind.True;
        result["skip"] = q.TryGetProperty("skip", out var skip) && skip.ValueKind == JsonValueKind.True;
        return result;
    }

    private static string Choices(JsonObject question)
    {
        var other = question["other"]?.GetValue<bool>() == true;
        var skip = question["skip"]?.GetValue<bool>() == true;
        return (other, skip) switch
        {
            (true, true) => "other: write your own answer; skip: leave it unanswered",
            (true, false) => "other: write your own answer",
            (false, true) => "skip: leave it unanswered",
            _ => "(pick one of the options)",
        };
    }

    private static string Untrusted(string text, bool fenced) => fenced && text.Length > 0 ? UntrustedFence.Wrap(Source, author: null, text) : text;

    private static string Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string Str(JsonObject node, string name) => node[name]?.GetValue<string>() ?? string.Empty;
}
