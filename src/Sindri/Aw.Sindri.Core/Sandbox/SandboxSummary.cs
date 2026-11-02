using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// The value-free summary of a session's sandbox plan (sandbox spec §6, §8): a field of <see cref="Launch.StartedLaunch"/>, which the engine
/// copies onto 'launch.started'. Paths are listed, contents never.
/// </summary>
/// <param name="Mechanism">What confined the session.</param>
/// <param name="PlanHash">The hash of the plan, which includes the resolved login source.</param>
/// <param name="Writable">The writable paths.</param>
/// <param name="Network">The allowed host group names.</param>
/// <param name="NetworkMode">Enforce or learn.</param>
/// <param name="Inner">Whether the agent CLI's own sandbox ran (resolved: on or off, never the profile's auto).</param>
/// <param name="Limits">The limits in force.</param>
/// <param name="Credentials">The GitHub credentials the session has.</param>
/// <param name="Login">The model login source the plan resolved to; fixed for the session's life.</param>
/// <param name="Attended">True for a session the owner opened with 'sindri open' (SB19); left out for a Step.</param>
public sealed record SandboxSummary(
    SandboxMechanism Mechanism,
    string PlanHash,
    IReadOnlyList<string> Writable,
    IReadOnlyList<string> Network,
    NetworkMode NetworkMode,
    InnerState Inner,
    SandboxLimitsSummary Limits,
    SandboxCredentialsSummary Credentials,
    ModelLoginSource Login,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Attended = false);
