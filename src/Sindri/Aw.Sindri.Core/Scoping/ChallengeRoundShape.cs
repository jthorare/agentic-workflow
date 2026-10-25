using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Scoping;

/// <summary>What 'fact:challengeRound' must be (schemas/scoping/challenge-round.json): one round, never the cumulative challenge log (#1102).</summary>
public static class ChallengeRoundShape
{
    private const string Fact = "fact:challengeRound";
    private const string LogFix = "report only this round, as schemas/scoping/challenge-round.json: walked, verdict, surfaces, behaviours; fact:challenge is the log of earlier rounds, not your output";

    /// <summary>Whether <paramref name="fact"/> is the one this check is for.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>True for 'fact:challengeRound'.</returns>
    public static bool Covers(FactKey fact) => string.Equals(fact.Value, Fact, StringComparison.Ordinal);

    /// <summary>The refusal when <paramref name="round"/> is a challenge log (an object with 'rounds' and no 'verdict'); null otherwise.</summary>
    /// <param name="round">The parsed fact.</param>
    /// <returns>The refusal text, or null.</returns>
    public static string? LogShape(JsonObject round)
    {
        ArgumentNullException.ThrowIfNull(round);
        return round["rounds"] is JsonArray && !round.ContainsKey("verdict")
            ? $"{Fact} is a challenge log, not a round (fix: {LogFix})"
            : null;
    }

    /// <summary>Why <paramref name="content"/> is a challenge log and not one round, in words an agent can fix; null for anything else (other shapes are left to the payload check and challenge-log).</summary>
    /// <param name="content">What the agent wrote.</param>
    /// <returns>The refusal text, or null.</returns>
    public static string? Violation(ReadOnlySpan<byte> content)
    {
        try
        {
            return JsonNode.Parse(Encoding.UTF8.GetString(content)) is JsonObject round ? LogShape(round) : null;
        }
        catch (JsonException)   // boundary: the content is what an agent wrote
        {
            return null;
        }
    }
}
