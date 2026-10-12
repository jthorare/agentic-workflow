namespace Aw.Sindri.Features.GetStatus;

/// <summary>The control plane's status, as both the CLI and the UI show it.</summary>
/// <param name="Running">Whether this process holds the host lease.</param>
/// <param name="Pid">Its process id.</param>
/// <param name="Epoch">The lease epoch it holds (0 when none).</param>
/// <param name="LeaseExpiresAt">When the grant lapses without a renewal.</param>
/// <param name="Profile">The approved and live profile.</param>
/// <param name="Services">Each scheduled service with its cadence and last run.</param>
public sealed record GetStatusResponse(
    bool Running,
    int Pid,
    long Epoch,
    DateTimeOffset? LeaseExpiresAt,
    ProfileStatus Profile,
    IReadOnlyList<ServiceStatus> Services);
