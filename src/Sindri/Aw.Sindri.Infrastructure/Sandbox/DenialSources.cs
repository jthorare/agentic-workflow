using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Where a finished session's denials are read from (sandbox spec §3.6).</summary>
/// <param name="Plan">The launch's plan (its mounts decide which failed paths are denials).</param>
/// <param name="Home">The session's $HOME inside the sandbox.</param>
/// <param name="TranscriptPath">The session's transcript or rollout, or null when it has none.</param>
/// <param name="Kind">The transcript's format.</param>
/// <param name="FailOpenLogPath">The per-launch <c lang="text">failopen.log</c>, or null.</param>
internal sealed record DenialSources(SandboxPlan Plan, string Home, string? TranscriptPath, TranscriptKind Kind, string? FailOpenLogPath);
