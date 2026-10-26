using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// What an engine notice says in a desktop banner (GitHub #1111): the stage in the app's words and what is asked of the owner, never the instance
/// address, an item id, a run token, a step id or a park reason code. The banner's title is the item's title (the notice effect adds it); the raw
/// address stays on the notice's line in 'notifications.jsonl'.
/// </summary>
internal static class NoticeWords
{
    /// <summary>The stage's name: the recipe state's display name when it has one, else its id made readable ('scope.challenge' is 'Scope challenge').</summary>
    /// <param name="state">The state id.</param>
    /// <param name="displayName">Reads a state's display name; null reads none.</param>
    /// <returns>The name.</returns>
    internal static string Stage(string state, Func<string, string?>? displayName)
    {
        if (displayName?.Invoke(state) is { Length: > 0 } named)
        {
            return named;
        }

        var spaced = state.Replace('.', ' ').Replace('-', ' ').Replace('_', ' ').Trim();
        return spaced.Length == 0 ? "This step" : char.ToUpper(spaced[0], CultureInfo.InvariantCulture) + spaced[1..];
    }

    /// <summary>A park: the stage and why it stopped, in words.</summary>
    /// <param name="stage">The stage's name.</param>
    /// <param name="reason">The park's reason.</param>
    /// <returns>The banner text.</returns>
    internal static string Park(string stage, ParkReason? reason) => $"{stage}: stopped, {Why(reason)}. Resume or retry it.";

    /// <summary>An escalation: the stage and that it needs a decision.</summary>
    /// <param name="stage">The stage's name.</param>
    /// <returns>The banner text.</returns>
    internal static string Escalation(string stage) => $"{stage}: needs a decision from you.";

    /// <summary>A desk task: the stage and that it waits for the owner.</summary>
    /// <param name="stage">The stage's name.</param>
    /// <returns>The banner text.</returns>
    internal static string DeskTask(string stage) => $"{stage}: waiting for you.";

    /// <summary>A takeover that sat idle.</summary>
    /// <param name="stage">The stage's name.</param>
    /// <returns>The banner text.</returns>
    internal static string IdleTakeover(string stage) => $"{stage}: still taken over. Hand it back or cancel it.";

    private static string Why(ParkReason? reason) => reason switch
    {
        ParkReason.Rounds => "it ran out of rounds",
        ParkReason.NoProgress => "it made no progress",
        ParkReason.Budget => "it hit its budget",
        ParkReason.NeedsAnswer => "it needs your answer",
        ParkReason.Timeout => "it timed out",
        ParkReason.SpecConflict => "the spec conflicts with the code",
        ParkReason.NoRuntime => "no runtime is available for it",
        ParkReason.Unavailable => "it needs a look",
        null or _ => "it needs a look",
    };
}
