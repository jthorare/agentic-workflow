using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>What a takeover opened: the launch's viewer record and the command that attaches to its session.</summary>
/// <param name="Viewer">The viewer outcome.</param>
/// <param name="Attach">The attach command.</param>
internal sealed record OpenedSession(LaunchViewer Viewer, IReadOnlyList<string> Attach);
