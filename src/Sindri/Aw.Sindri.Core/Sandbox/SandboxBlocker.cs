namespace Aw.Sindri.Core.Sandbox;

/// <summary>One reason this box can't confine a Step right now, as the Check again card lists it (sandbox spec §6, §8).</summary>
/// <param name="Code">The SND-LAUNCH code (-014 to -017).</param>
/// <param name="Reason">The code's reason (<see cref="SandboxReasons"/>), which picks its §8 sentence.</param>
/// <param name="Source">For -017 the credential source ('owner-copy' or 'separate-login'), which picks its sentence; null for every other code.</param>
/// <param name="Values">The named values the sentence leaves open ('which', 'tool', 'path', 'target'), by name; null when there are none.</param>
/// <param name="Detail">What the refusal observed this time (the canary's output, the failing tool line), already scrubbed; null when it said nothing more.</param>
public sealed record SandboxBlocker(SandboxCode Code, string Reason, string? Source = null, IReadOnlyDictionary<string, string>? Values = null, string? Detail = null)
{
    private static readonly string[] _named = ["which", "tool", "path", "target", "cap", "running", "limit", "time", "name", "status"];

    /// <summary>Makes the blocker of a sandbox refusal.</summary>
    /// <param name="refusal">An error made by <see cref="SandboxRefusal"/>; any other error is told as an unsupported platform, so a check never hides a failure.</param>
    /// <returns>The blocker.</returns>
    public static SandboxBlocker Of(Kernel.Error refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        var code = SandboxCodes.All.FirstOrDefault(c => string.Equals(c.Error.Code, refusal.Code, StringComparison.Ordinal));
        var reason = SandboxRefusal.ReasonOf(refusal);
        if (code is null || reason is null)
        {
            return new SandboxBlocker(SandboxCodes.All.First(static c => ReferenceEquals(c.Error, SindriErrors.LaunchSandboxUnavailable)), SandboxReasons.UnsupportedPlatform);
        }

        var values = _named
            .Select(name => (name, value: refusal.Extensions?.Where(e => string.Equals(e.Key, name, StringComparison.Ordinal) && e.Value.ValueKind == System.Text.Json.JsonValueKind.String).Select(e => e.Value.GetString()).FirstOrDefault()))
            .Where(static x => x.value is not null)
            .ToDictionary(static x => x.name, static x => x.value ?? string.Empty, StringComparer.Ordinal);
        return new SandboxBlocker(code, reason, SandboxRefusal.SourceTextOf(refusal), values.Count == 0 ? null : values, DetailOf(refusal, reason));
    }

    /// <summary>What <see cref="SandboxRefusal.For"/> appended after the reason: "Title (reason): detail".</summary>
    private static string? DetailOf(Kernel.Error refusal, string reason)
    {
        var marker = $"({reason}): ";
        var at = refusal.Message.IndexOf(marker, StringComparison.Ordinal);
        return at < 0 || at + marker.Length >= refusal.Message.Length ? null : refusal.Message[(at + marker.Length)..];
    }
}
