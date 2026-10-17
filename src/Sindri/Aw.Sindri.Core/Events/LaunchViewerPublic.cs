using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of <see cref="LaunchViewer"/>: without the viewer's stderr.</summary>
/// <param name="Kind">The viewer kind.</param>
/// <param name="Label">What a person calls it, or null.</param>
/// <param name="Outcome">What it did.</param>
/// <param name="Code">The error code for a failure, otherwise null.</param>
public sealed record LaunchViewerPublic(string Kind, string? Label, ViewerOutcomeKind Outcome, string? Code);
