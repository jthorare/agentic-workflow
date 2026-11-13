using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Run the health checks. Runs in the calling process, read-only, so it works when the control plane is down.</summary>
public sealed record RunDoctorRequest
{
    /// <summary>Called with each row as soon as it and every row before it are known (#3390), in the response's order.</summary>
    public Action<DoctorCheck>? OnRow { get; init; }

    /// <summary>The pinned harness probe's rows (engine spec §6.2), run in the same parallel pass as the other checks and listed last (#3390); null runs none.</summary>
    public Func<CancellationToken, ValueTask<IReadOnlyList<DoctorCheck>>>? Harnesses { get; init; }
}
