using System.Text.Json.Nodes;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>The --record file's lines: one JSON object per answer, {itemId, label, status, decision, confidence, reasonCode, latencyMs}.</summary>
internal static class EvalRecordLine
{
    private const string Decided = "decided";
    private const string Unavailable = "unavailable";
    private const string Errored = "errored";

    internal static JsonObject ToNode(EvalResult row)
    {
        var node = new JsonObject { ["itemId"] = row.ItemId, ["label"] = row.Label };
        switch (row.Answer)
        {
            case ProviderDecided decided:
                node["status"] = Decided;
                node["decision"] = decided.Decision;
                node["confidence"] = decided.Confidence;
                break;
            case ProviderUnavailable:
                node["status"] = Unavailable;
                break;
            default:
                node["status"] = Errored;
                break;
        }

        node["reasonCode"] = row.Answer.ReasonCode;
        node["latencyMs"] = row.LatencyMs;
        return node;
    }

    /// <summary>Reads one line; null when it isn't a record.</summary>
    internal static EvalResult? TryRead(string line)
    {
        if (!InputJson.TryParse(line, out var parsed) || parsed is not JsonObject node)
        {
            return null;
        }

        try
        {
            return Read(node);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static EvalResult? Read(JsonObject node)
    {
        if (node["itemId"]?.GetValue<string>() is not { } itemId)
        {
            return null;
        }

        return Answer(node, Text(node, "reasonCode")) is { } answer
            ? new EvalResult(itemId, Text(node, "label"), answer, node["latencyMs"]?.GetValue<long>() ?? 0)
            : null;
    }

    private static string Text(JsonObject node, string name) => node[name]?.GetValue<string>() ?? "";

    private static ProviderResult? Answer(JsonObject node, string reason) => node["status"]?.GetValue<string>() switch
    {
        Decided when node["decision"]?.GetValue<string>() is { } decision && node["confidence"] is { } confidence =>
            new ProviderDecided(decision, confidence.GetValue<double>(), reason),
        Unavailable => new ProviderUnavailable(reason),
        Errored => new ProviderErrored(reason),
        _ => null,
    };
}
