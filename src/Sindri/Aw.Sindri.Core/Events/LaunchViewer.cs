using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Events;

/// <summary>The viewer part of 'launch.started' (launcher L-10). <see cref="Detail"/> is the viewer's redacted stderr and stays out of <see cref="LaunchViewerPublic"/>.</summary>
/// <param name="Kind">The viewer kind ('warp', 'exec-template', 'print', or 'none' for an unattended launch).</param>
/// <param name="Label">What a person calls it, or null.</param>
/// <param name="Outcome">What it did.</param>
/// <param name="Code">The error code for a failure, otherwise null.</param>
/// <param name="Detail">The viewer's redacted stderr for a failure, otherwise null.</param>
public sealed record LaunchViewer(string Kind, string? Label, ViewerOutcomeKind Outcome, string? Code, string? Detail);
