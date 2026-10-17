using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'session.ended' payload (API spec §8.1). Key 'session.ended:&lt;launch&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Reason">Exit or lost.</param>
/// <param name="ExitCode">The agent's exit code for <see cref="SessionEndReason.Exit"/>, otherwise null.</param>
public sealed record SessionEnded(LaunchId Launch, SessionEndReason Reason, int? ExitCode);
