using System.Text.RegularExpressions;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// wake-gate: should an agent's message wake its orchestrator now ('send'), wait in the queue ('batch') or be dropped
/// ('drop')? Pure acks and very short text drop, urgent words send, and an unsure model sends (as before the judge).
/// </summary>
public static partial class WakeGateQuestion
{
    /// <summary>The question's name.</summary>
    public const string Name = "wake-gate";

    /// <summary>Deliver now.</summary>
    public const string Send = "send";

    /// <summary>Queue it.</summary>
    public const string Batch = "batch";

    /// <summary>Drop it.</summary>
    public const string Drop = "drop";

    /// <summary>Text this short or shorter (trimmed) carries nothing worth a wake-up.</summary>
    public const int ShortMessageLength = 12;

    /// <summary>The definition.</summary>
    public static QuestionDefinition<WakeGateInput> Definition { get; } = new()
    {
        Name = Name,
        Outputs = [Send, Batch, Drop],
        Criteria = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Send] = "a result, failure, blocker, question, or plan change that should be delivered immediately",
            [Batch] = "progress with nothing to act on, to be queued",
            [Drop] = "a pure acknowledgement with no content",
        },
        ContentClass = ContentClass.MessageMeta,
        Threshold = 0.7,
        TimeBudget = TimeSpan.FromSeconds(10),
        Prompt = Prompt,
        PreRules = PreRule,
        FallbackRules = static _ => Send,
    };

    internal static string? PreRule(WakeGateInput input)
    {
        var trimmed = input.Text.Trim();
        if (AckJson.IsMatch(trimmed))
        {
            return Drop;
        }

        if (Urgent.IsMatch(trimmed))
        {
            return Send;
        }

        return trimmed.Length <= ShortMessageLength ? Drop : null;
    }

    private static string Prompt(WakeGateInput input) =>
        $"An agent wants to send this message to its orchestrator: \"{input.Text}\". "
        + "Classify it as one of: \"send\" (a result, failure, blocker, question, or plan change: deliver it now), "
        + "\"batch\" (progress with nothing to act on: queue it), or \"drop\" (a pure acknowledgement with no content). "
        + "Respond with only the JSON {\"decision\": \"send\"|\"batch\"|\"drop\"}.";

    // A structured ack or idle notice ({"type":"ack"...}).
    [GeneratedRegex(@"^\s*\{\s*""type""\s*:\s*""(ack|idle_notification)""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AckJson { get; }

    [GeneratedRegex(@"\b(blocked|blocker|failed|error|question|approve|approval)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Urgent { get; }
}
