using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The judge port over the judge binary (cutover spec §4.3 b): judge resolution-check with the input JSON on stdin, then
/// judge why &lt;id&gt; for the stored input digest, each bounded to 5 minutes. The binary is AW_JUDGE_BIN else PATH, and it
/// reads the judge state under AW_JUDGE_STATE_DIR else AW_STATE_DIR. The input file the ledger wrote already carries the judge's
/// check kind, and the answer names the judge's 16-hex input digest.
/// </summary>
internal sealed class JudgeCli(Func<string, string?> environment) : IJudge
{
    private const int Escalated = 2;
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(5);

    public Result<JudgeAnswer> ResolutionCheck(JudgeQuestion question)
    {
        var (binary, missing) = ToolBinary.Find(environment, "AW_JUDGE_BIN", "judge");
        if (binary is null)
        {
            return Unavailable(missing);
        }

        var asked = Processes.Capture(ToolBinary.Start(environment, binary, ["resolution-check"]), _timeout, File.ReadAllText(question.InputFile));
        return asked.ExitCode switch
        {
            0 => Parse(asked.Output).Bind(envelope => Decided(binary, envelope)),
            Escalated => Parse(asked.Output).Bind(Reason).Map(code => new JudgeAnswer(JudgeDecision.Escalated, DecisionId: null, code, Reasons: [], question.InputSha256)),
            _ => Unavailable(string.Create(CultureInfo.InvariantCulture, $"judge resolution-check exited {asked.ExitCode}: {Tail(asked.Error)}")),
        };
    }

    private Result<JudgeAnswer> Decided(string binary, JsonNode envelope)
    {
        var decision = envelope["decision"]?.GetValue<string>() switch
        {
            "resolved" => JudgeDecision.Resolved,
            "partial" => JudgeDecision.Partial,
            "unresolved" => JudgeDecision.Unresolved,
            _ => (JudgeDecision?)null,
        };
        if (decision is not { } value || envelope["id"]?.GetValue<string>() is not { } id || !JudgeDecisionId.TryFrom(id, out var decisionId))
        {
            return Unavailable($"judge resolution-check answered without a decision and an id: {Tail(envelope.ToJsonString())}");
        }

        return StoredDigest(binary, id).Map(digest => new JudgeAnswer(value, decisionId, envelope["reasonCode"]?.GetValue<string>() ?? string.Empty, Reasons(envelope), digest));
    }

    private Result<string> StoredDigest(string binary, string id)
    {
        var why = Processes.Capture(ToolBinary.Start(environment, binary, ["why", id]), _timeout);
        var stored = why.ExitCode == 0 ? Parse(why.Output) : null;
        return stored is { IsSuccess: true } && stored.Value["input_digest"]?.GetValue<string>() is { } digest
            ? digest
            : Unavailable<string>(string.Create(CultureInfo.InvariantCulture, $"judge why {id} gave no input digest (exit {why.ExitCode}): {Tail(why.Error)}"));
    }

    private static List<string> Reasons(JsonNode envelope) =>
        envelope["reasons"] is JsonArray array ? [.. array.Select(static r => r?.GetValue<string>() ?? string.Empty)] : [];

    private static Result<string> Reason(JsonNode envelope) =>
        envelope["reasonCode"]?.GetValue<string>() is { } code ? code : Unavailable<string>("judge answered without a reasonCode");

    private static Result<JsonNode> Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text) is JsonObject obj ? obj : Unavailable<JsonNode>("judge printed JSON that is not an object");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return Unavailable<JsonNode>($"judge printed no JSON envelope: {Tail(text)}");
        }
    }

    private static Result<JudgeAnswer> Unavailable(string message) => Unavailable<JudgeAnswer>(message);

    private static Result<T> Unavailable<T>(string message) => BugFixErrors.JudgeUnavailable.With($"{message}; no verdict recorded");

    private static string Tail(string text) => text.Length <= 200 ? text.Trim() : text[..200].Trim();
}
