using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The structured part of 'launch.failed' (launcher L-1, L-3): where the launch stopped, and the facts an error carried as extensions
/// (SND-LAUNCH-012's reason, current and approved hashes; a sandbox refusal's reason and credential source). Members with nothing to say are written as null.
/// </summary>
/// <param name="Stage">Where the launch stopped ('host' or 'backstop'), or null for a failure before the launcher ran.</param>
/// <param name="Reason">The error's 'reason' extension (not-approved or hash-changed for -012; 'no-bwrap', 'expired', ... for the sandbox codes -014 to -021), or null. The one place the launch's why lives.</param>
/// <param name="Current">The error's 'current' extension (the entry's argv hash), or null.</param>
/// <param name="Approved">The error's 'approved' extension (the hashes the profile lists), or null.</param>
/// <param name="Source">The error's 'source' extension (the credential source of SND-LAUNCH-017: 'owner-copy' or 'separate-login'); left out when null.</param>
public sealed record LaunchFailureDetail(LaunchStage? Stage, string? Reason = null, string? Current = null, IReadOnlyList<string>? Approved = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null)
{
    /// <summary>The detail of a failure: the stage and what <paramref name="error"/> carries, or null when there is neither.</summary>
    /// <param name="error">The error.</param>
    /// <param name="stage">The stage, or null.</param>
    /// <returns>The detail, or null.</returns>
    public static LaunchFailureDetail? Of(Error error, LaunchStage? stage)
    {
        ArgumentNullException.ThrowIfNull(error);
        var reason = Text(error, "reason");
        var current = Text(error, "current");
        var source = Text(error, "source");
        var approved = error.Extensions is { } extensions && extensions.FirstOrDefault(static e => string.Equals(e.Key, "approved", StringComparison.Ordinal)) is { Key: not null } found
            && found.Value.ValueKind == System.Text.Json.JsonValueKind.Array
            ? (IReadOnlyList<string>)[.. found.Value.EnumerateArray().Select(static v => v.GetString() ?? string.Empty)]
            : null;
        return stage is null && reason is null && current is null && approved is null && source is null ? null : new LaunchFailureDetail(stage, reason, current, approved, source);
    }

    private static string? Text(Error error, string name) =>
        error.Extensions is { } extensions && extensions.FirstOrDefault(e => string.Equals(e.Key, name, StringComparison.Ordinal)) is { Key: not null } found
            && found.Value.ValueKind == System.Text.Json.JsonValueKind.String
            ? found.Value.GetString()
            : null;
}
