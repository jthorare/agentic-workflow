using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxStatus;

/// <summary>The Onyx status: the pins, the preflight's checks and the exit code they add up to.</summary>
/// <param name="State">Where Onyx stands: not-set-up (P2 adds running and stopped).</param>
/// <param name="PinnedVersion">The pinned Onyx release.</param>
/// <param name="PinnedCommit">The commit its tag points to.</param>
/// <param name="Checks">docker, compose, memory, disk, port, in that order.</param>
/// <param name="Port">The port setup would use.</param>
/// <param name="ExitCode">0 when every check is ok, otherwise 1.</param>
public sealed record OnyxStatusResponse(OnyxState State, string PinnedVersion, string PinnedCommit, IReadOnlyList<PreflightCheck> Checks, int Port, int ExitCode);
