using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Views;

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
    IReadOnlyList<ServiceStatus> Services)
{
    /// <summary>The API version this process serves (API spec §9); the app compares it with its own on connect.</summary>
    public int ApiVersion { get; init; }

    /// <summary>The projector's status (API spec §2.2): cursor, head, rebuilding and stall.</summary>
    public ViewsStatus? Views { get; init; }

    /// <summary>The one-word health and its problems (UI spec E-14).</summary>
    public HealthReport? Health { get; init; }

    /// <summary>What the session collector sees (collector spec CA-8): additive, for the tray tooltip and the Agents tab count.</summary>
    public SessionStatus? Sessions { get; init; }

    /// <summary>The running and the installed build (#3052): the app offers Restart when <see cref="BuildStatus.Stale"/>.</summary>
    public BuildStatus? Build { get; init; }
}
