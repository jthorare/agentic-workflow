using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxStatus;

/// <summary>The Onyx status: where it stands, the pins, the preflight's checks and the exit code they add up to.</summary>
/// <param name="State">not-set-up until `sindri onyx up` finished its start step and the files are still there; then unknown when Docker isn't usable here, else running when nginx and the API server run, otherwise stopped.</param>
/// <param name="PinnedVersion">The pinned Onyx release.</param>
/// <param name="PinnedCommit">The commit its tag points to.</param>
/// <param name="Checks">docker, compose, memory, disk, port, in that order (the port row says "Onyx's own" while it is running there).</param>
/// <param name="Port">The port Onyx is on while running, otherwise the one setup would use.</param>
/// <param name="ExitCode">0 when every check is ok, otherwise 1 (the checks only: stopped or not set up isn't a failure).</param>
/// <param name="Url">http://127.0.0.1:&lt;port&gt; while running, otherwise null.</param>
/// <param name="RunningVersion">The release the running Onyx reports, or unreachable when it doesn't answer; null when it isn't running.</param>
/// <param name="Detail">What to do next, per state.</param>
public sealed record OnyxStatusResponse(
    OnyxState State, string PinnedVersion, string PinnedCommit, IReadOnlyList<PreflightCheck> Checks, int Port, int ExitCode, string? Url, string? RunningVersion, string? Detail);
