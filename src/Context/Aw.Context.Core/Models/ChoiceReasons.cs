namespace Aw.Context.Core.Models;

/// <summary>The reason codes an <see cref="IChoicePort"/> failure carries as its message (never provider text), and the ones the labeler stays quiet about.</summary>
public static class ChoiceReasons
{
    /// <summary>The approved profile's pick is off: nothing was asked.</summary>
    public const string Off = "off";

    /// <summary>The pick is on but there is no Jev key: nothing was asked.</summary>
    public const string NoKey = "no-key";

    /// <summary>The pack was more characters than the port may send.</summary>
    public const string OverCap = "over-cap";

    /// <summary>Whether the reason means the pick was never in force (so the build is not degraded by it).</summary>
    /// <param name="reason">The reason code.</param>
    /// <returns>True for 'off' and 'no-key'.</returns>
    public static bool IsQuiet(string reason) => string.Equals(reason, Off, StringComparison.Ordinal) || string.Equals(reason, NoKey, StringComparison.Ordinal);
}
