using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.GetSandboxStatus;

/// <summary>The sandbox as the last check saw it, with its configuration (sandbox spec §8): the Health row and the Settings section.</summary>
/// <param name="CheckedAt">When the last check ran; null (left out) when none has run since serve started.</param>
/// <param name="Ready">True when nothing blocked a Step at that check; null (left out) when none has run.</param>
/// <param name="Blockers">Every blocker that check found, none when it passed or none has run.</param>
/// <param name="Mechanism">'bubblewrap', 'seatbelt' or 'none'.</param>
/// <param name="Groups">The network group names a session may reach (built in, then the profile's).</param>
/// <param name="Limits">The limits a Step runs under.</param>
/// <param name="Login">The model login source.</param>
/// <param name="Learn">The repos whose network learn mode is open.</param>
public sealed record SandboxStatusResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? CheckedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Ready,
    IReadOnlyList<SandboxBlockerLine> Blockers,
    string Mechanism,
    IReadOnlyList<string> Groups,
    SandboxLimitsSummary Limits,
    ModelLoginSource Login,
    IReadOnlyList<SandboxLearnLine> Learn);
