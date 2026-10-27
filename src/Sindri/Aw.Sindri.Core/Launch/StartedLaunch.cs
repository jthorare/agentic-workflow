using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Launch;

/// <summary>The session exists. A viewer that failed is still a start: the session keeps running (launcher SL5).</summary>
/// <param name="Host">The session in its host.</param>
/// <param name="Viewer">What the viewer did.</param>
/// <param name="Attach">The command that attaches to the session (shown when the viewer didn't open).</param>
/// <param name="CatalogSource">Where the launched entry came from.</param>
/// <param name="ArgvHash">The hash of the entry's argv template, as the profile's stepArgvHash holds it.</param>
public sealed record StartedLaunch(HostRef Host, ViewerOutcome Viewer, IReadOnlyList<string> Attach, CatalogSource CatalogSource, string ArgvHash) : LaunchResult
{
    /// <summary>The session id the launcher chose for the agent before it started (the entry's 'pre-chosen-uuid' mode), or null when the id is reported later (launcher L-9).</summary>
    public string? ChosenSession { get; init; }

    /// <summary>The sandbox summary the engine copies onto 'launch.started' (sandbox spec §6), or null before SB-5a and for an attended open before SB-5b.</summary>
    public SandboxSummary? Sandbox { get; init; }
}
