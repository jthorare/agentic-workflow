namespace Aw.Meta.Core.Routing;

/// <summary>Reads Codex's usage windows from the end of a rollout file.</summary>
public static class CodexRateLimits
{
    /// <summary>Finds the last rate_limits snapshot.</summary>
    /// <param name="lines">The rollout's last lines, oldest first.</param>
    /// <param name="now">The current time; a window whose reset time has passed counts as unused.</param>
    /// <returns>The signal, or null when no line carries one.</returns>
    public static ProviderSignal? Read(IReadOnlyList<string> lines, DateTimeOffset now) => throw new NotSupportedException();
}
