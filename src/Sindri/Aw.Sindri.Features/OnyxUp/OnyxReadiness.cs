using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>Waits for the started stack to answer: polls the health route and then the version every 2 s, up to 15 minutes. Another version than the pinned one, or no answer in time, is SND-ONYX-005.</summary>
internal sealed class OnyxReadiness(IOnyxHealth health, IContainerRuntime runtime, TimeProvider clock)
{
    /// <summary>The gap between polls.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    /// <summary>How long the stack gets to come up (the first start pulls models and runs migrations).</summary>
    internal static readonly TimeSpan Deadline = TimeSpan.FromMinutes(15);

    /// <summary>Compares two release tags, ignoring a leading v.</summary>
    /// <param name="reported">The version Onyx reported.</param>
    /// <param name="pinned">The pinned version.</param>
    /// <returns>True when they name the same release.</returns>
    internal static bool SameVersion(string reported, string pinned)
        => string.Equals(reported.TrimStart('v'), pinned.TrimStart('v'), StringComparison.Ordinal);

    /// <summary>Polls until Onyx reports a version.</summary>
    /// <param name="context">The run's shared facts (port, pinned version, project).</param>
    /// <param name="progress">Told one dot per poll.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <param name="limit">How long to wait; the full <see cref="Deadline"/> when null (a start that already spent some of it passes the rest).</param>
    /// <returns>The version it reported, or SND-ONYX-005.</returns>
    internal async ValueTask<Result<string>> Wait(OnyxUpContext context, IOnyxProgress progress, CancellationToken cancellationToken, TimeSpan? limit = null)
    {
        var allowed = limit ?? Deadline;
        var start = clock.GetUtcNow();
        while (true)
        {
            var probe = await health.Probe(context.Port, cancellationToken).ConfigureAwait(false);
            progress.Tick();
            if (probe is { Healthy: true, BackendVersion: { } version })
            {
                return SameVersion(version, context.Pins.Version)
                    ? version
                    : SindriErrors.OnyxNotReady.With($"Onyx answered with version {version} but this Sindri is pinned to {context.Pins.Version}; run `sindri onyx down`, remove Sindri's Onyx files under the Sindri home, and run `sindri onyx up` again");
            }

            if (clock.GetUtcNow() - start >= allowed)
            {
                return TimedOut(context, cancellationToken);
            }

            await Task.Delay(Interval, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The SND-ONYX-005 for a stack that didn't come up within the deadline.</summary>
    /// <param name="context">The run's shared facts.</param>
    /// <param name="cancellationToken">Cancels the container listing.</param>
    /// <returns>The error naming each container's state.</returns>
    internal Error TimedOut(OnyxUpContext context, CancellationToken cancellationToken)
        => SindriErrors.OnyxNotReady.With(string.Create(CultureInfo.InvariantCulture, $"Onyx didn't answer within {Deadline.TotalMinutes:0} minutes ({Summary(context, cancellationToken)}); check `docker compose -p {context.Project.Name} logs`, give Docker more memory or disk if a service keeps restarting, then run `sindri onyx up` again"));

    // The containers as `service: state (health)` joined by `; `, so the message says which one never came up.
    private string Summary(OnyxUpContext context, CancellationToken cancellationToken)
    {
        var containers = runtime.Ps(context.Project, cancellationToken);
        return containers.IsError
            ? "docker couldn't list the containers"
            : string.Join("; ", containers.Value.OrderBy(static c => c.Service, StringComparer.Ordinal).Select(static c => c.Health is { } h ? $"{c.Service}: {c.State} ({h})" : $"{c.Service}: {c.State}"));
    }
}
