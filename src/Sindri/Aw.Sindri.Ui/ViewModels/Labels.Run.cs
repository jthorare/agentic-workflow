namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): an agent running a Step.</summary>
internal static partial class Labels
{

    /// <summary>The launch was asked for and has not started.</summary>
    internal const string LaunchRequested = "The agent session is starting.";

    /// <summary>A launch that failed.</summary>
    /// <param name="code">The failure code.</param>
    /// <param name="detail">The sentence with its fix.</param>
    /// <returns>The sentence.</returns>
    internal static string LaunchFailed(string code, string detail) =>
        detail.Length == 0 ? $"The agent session failed to start ({code})." : $"The agent session failed to start ({code}): {detail}";

    /// <summary>A failed agent Step.</summary>
    /// <param name="reason">The served reason (launch, lost, exit, limit).</param>
    /// <param name="to">The state the instance moved to, when served.</param>
    /// <returns>The sentence.</returns>
    internal static string AgentFailed(string? reason, string? to)
    {
        var why = reason switch
        {
            "launch" => "The agent couldn't start",
            "lost" => "The agent session was lost",
            "exit" => "The agent exited",
            "idle" => "The agent stopped without reporting",
            "limit" => "The agent hit its limit",
            _ => "The agent's step failed",
        };
        return string.IsNullOrEmpty(to) ? why + "." : $"{why}. The run moved to {to}.";
    }

}
