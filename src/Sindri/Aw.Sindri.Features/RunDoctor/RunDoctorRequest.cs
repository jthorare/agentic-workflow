namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Run the health checks. Runs in the calling process, read-only, so it works when the control plane is down.</summary>
public sealed record RunDoctorRequest;
