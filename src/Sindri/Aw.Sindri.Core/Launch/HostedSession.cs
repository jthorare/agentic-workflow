using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>One session an <see cref="IHostedSessions"/> host reports.</summary>
/// <param name="Launch">The launch the session belongs to.</param>
/// <param name="Alive">True while the session exists (or its host can't be asked); false once it is gone.</param>
public sealed record HostedSession(LaunchId Launch, bool Alive);
