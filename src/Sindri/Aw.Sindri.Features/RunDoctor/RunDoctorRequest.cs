using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Run the health checks. Runs in the calling process, read-only, so it works when the control plane is down.</summary>
public sealed record RunDoctorRequest
{
    /// <summary>Called with each row as soon as it and every row before it are known (#3390), in the response's order.</summary>
    public Action<DoctorCheck>? OnRow { get; init; }

    /// <summary>How long one check class may run before its row becomes a "took over" warning and it is skipped; null means 20 seconds.</summary>
    public TimeSpan? Cap { get; init; }
}
