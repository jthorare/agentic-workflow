using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Judge;

/// <summary>
/// <see cref="IJudgeChoice"/> over the judge program (AW_SINDRI_JUDGE, else 'judge' on the PATH), run through <see cref="IToolRunner"/> as 'judge choose --providers a,b' with the question on standard input (DT spec 8.3).
/// It runs with AW_JUDGE_STATE_DIR set to Sindri's own judge directory, so its decision rows never join the hooks' store. Exit 0 is a decided envelope and exit 2 an escalated one; anything else, a missing program or a timeout is a failed answer.
/// </summary>
/// <param name="runner">Runs the judge.</param>
/// <param name="environment">Reads AW_SINDRI_JUDGE.</param>
/// <param name="stateDirectory">The directory for the judge's own store (AW_JUDGE_STATE_DIR).</param>
internal sealed class JudgeCliChoice(IToolRunner runner, Func<string, string?> environment, string stateDirectory) : IJudgeChoice
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(100);   // under the Step's two minutes, over the judge's own 15 s provider budget

    public ValueTask<ChooseAnswer> Choose(ChooseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var program = environment("AW_SINDRI_JUDGE") is { Length: > 0 } given ? given : "judge";
        var options = new ToolRunOptions(Environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["AW_JUDGE_STATE_DIR"] = stateDirectory }, StandardInput: Input(request));
        var run = runner.Run(program, ["choose", "--providers", string.Join(',', request.Providers)], _timeout, options, cancellationToken);
        return ValueTask.FromResult(Answer(run, program));
    }

    private static string Input(ChooseRequest request)
    {
        var state = new JsonObject();
        foreach (var (key, text) in request.Facts)
        {
            state[key] = text;
        }

        return new JsonObject
        {
            ["prompt"] = request.Prompt,
            ["state"] = state,
            ["choices"] = new JsonArray([.. request.Choices.Select(static c => (JsonNode)new JsonObject { ["name"] = c.Name, ["criterion"] = c.Criterion })]),
            ["threshold"] = request.Threshold,
        }.ToJsonString();
    }

    private static ChooseAnswer Answer(ToolRun run, string program)
    {
        if (run.Outcome != ToolOutcome.Exited)
        {
            return Failed($"judge choose did not finish ({program}: {run.Outcome}); fix: build judge and put it on the PATH, or set AW_SINDRI_JUDGE");
        }

        if (run.ExitCode is not (0 or 2))
        {
            return Failed($"judge choose exited {run.ExitCode.ToString(CultureInfo.InvariantCulture)}: {Tail(run.Error.Length > 0 ? run.Error : run.Output)}");
        }

        var envelope = Parse(run.Output);
        if (envelope is null)
        {
            return Failed($"judge choose printed no JSON envelope: {Tail(run.Output)}");
        }

        var reason = Text(envelope, "reasonCode");
        if (run.ExitCode == 2)
        {
            return Escalation(envelope, reason, run.Output);
        }

        return Decided(envelope, reason, run.Output);
    }

    private static ChooseAnswer Decided(JsonObject envelope, string? reason, string output) =>
        Text(envelope, "decision") is { } choice && Text(envelope, "model") is { } provider && envelope["confidence"] is JsonValue confidence && confidence.TryGetValue<double>(out var value)
            ? new ChooseAnswer(ChooseStatus.Decided, choice, reason ?? string.Empty, value, provider, Text(envelope, "id"), Probabilities(envelope))
            : Failed($"judge choose printed a decided envelope without a decision, model or confidence: {Tail(output)}");

    // invalid-input and question-disabled are the judge refusing the question, not being unsure: the Step fails (DT spec 3.1).
    private static ChooseAnswer Escalation(JsonObject envelope, string? reason, string output)
    {
        if (envelope["escalated"] is not JsonValue flag || !flag.TryGetValue<bool>(out var escalated) || !escalated || reason is null)
        {
            return Failed($"judge choose exited 2 without an escalated envelope: {Tail(output)}");
        }

        return string.Equals(reason, "invalid-input", StringComparison.Ordinal) || string.Equals(reason, "question-disabled", StringComparison.Ordinal)
            ? new ChooseAnswer(ChooseStatus.Failed, Choice: null, reason, DecisionRef: Text(envelope, "id"))
            : new ChooseAnswer(ChooseStatus.Escalated, Choice: null, reason, DecisionRef: Text(envelope, "id"), Probabilities: Probabilities(envelope));
    }

    private static ChooseAnswer Failed(string reason) => new(ChooseStatus.Failed, Choice: null, reason);

    private static JsonObject? Parse(string output)
    {
        try
        {
            return JsonNode.Parse(output) as JsonObject;
        }
        catch (JsonException)
        {
            return null;   // boundary: the judge printed something else
        }
    }

    private static string? Text(JsonObject json, string name) => json[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static Dictionary<string, double>? Probabilities(JsonObject json) => json["probabilities"] is JsonObject map
        ? map.Select(static e => (e.Key, Value: e.Value is JsonValue v && v.TryGetValue<double>(out var p) ? p : (double?)null)).Where(static e => e.Value is not null).ToDictionary(static e => e.Key, static e => e.Value.GetValueOrDefault(), StringComparer.Ordinal)
        : null;

    private static string Tail(string text) => text.Trim() is { Length: > 300 } trimmed ? trimmed[^300..] : text.Trim();
}
