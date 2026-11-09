using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The refusals of the Linear write gate, with the texts the issue sheet shows (Linear hub spec §8).</summary>
internal static class LinearWriteRefusals
{
    /// <summary>SND-AUTH-002: how a session refusal that Context itself gave (SND-CONTEXT-020) is shown.</summary>
    /// <returns>The error.</returns>
    internal static Error Session() => SindriErrors.AuthRefused.With("Linear writes aren't open to this caller (fix: the owner turns on context.sessions.enabled in the profile, or uses the app)");

    /// <summary>SND-LINEAR-002: no usable write key.</summary>
    /// <returns>The error.</returns>
    internal static Error NoWriteKey() => SindriErrors.LinearNoWriteKey.With("Sindri has no Linear write key (fix: Settings, Connections, Linear, add a write key)");

    /// <summary>SND-LINEAR-003: the team is not one the profile picked.</summary>
    /// <param name="team">The team's key.</param>
    /// <returns>The error.</returns>
    internal static Error TeamNotPicked(string team) => SindriErrors.LinearTeamNotPicked.With($"Sindri only writes to teams you picked; {team} isn't one (fix: Settings, Connections, Linear, add {team})");

    /// <summary>The refusal a gate code stands for.</summary>
    /// <param name="code">SND-AUTH-002, SND-LINEAR-002 or SND-LINEAR-003.</param>
    /// <param name="team">The team's key.</param>
    /// <returns>The error, or null for any other code.</returns>
    internal static Error? Of(string code, string team) => code switch
    {
        "SND-AUTH-002" => Session(),
        "SND-LINEAR-002" => NoWriteKey(),
        "SND-LINEAR-003" => TeamNotPicked(team),
        _ => null,
    };
}
