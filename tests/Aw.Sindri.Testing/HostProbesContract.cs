using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IHostProbes"/> honours: a path that doesn't exist yet is measured at its nearest existing
/// ancestor, a bound port is reported taken and a free one free, and the fallback is the first free port of the range.
/// Run against the fake and the real adapter (over a temp directory and real loopback listeners).
/// </summary>
public abstract class HostProbesContract
{
    /// <summary>A world with a port that can't be bound, one that can, a range of taken ports, and a directory that exists.</summary>
    /// <param name="TakenPort">A port nothing can bind.</param>
    /// <param name="FreePort">A port that can be bound once <paramref name="ReleaseFreePort"/> has run (until then the scene holds it, so nothing else can take it).</param>
    /// <param name="Directory">A directory that exists.</param>
    /// <param name="Probes">The probes over that world.</param>
    /// <param name="TakenRangeFirst">The first port of a range whose every port is taken.</param>
    /// <param name="TakenRangeLast">The last port of that range, inclusive.</param>
    /// <param name="ReleaseFreePort">Releases <paramref name="FreePort"/> just before it is probed (safe to call twice).</param>
    protected sealed record Scene(int TakenPort, int FreePort, string Directory, IHostProbes Probes, int TakenRangeFirst, int TakenRangeLast, Action ReleaseFreePort);

    /// <summary>Builds a scene; the contract disposes the returned handle when the case ends.</summary>
    /// <param name="holder">What keeps the scene alive (listeners, temp directories).</param>
    /// <returns>The scene.</returns>
    protected abstract Scene Create(out IDisposable holder);

    /// <summary>A path below an existing directory is measured at that directory.</summary>
    [Fact]
    public void A_missing_path_is_measured_at_its_nearest_existing_ancestor()
    {
        var scene = Create(out var holder);
        using var _ = holder;

        var facts = scene.Probes.FreeDisk(Path.Combine(scene.Directory, "absent", "deeper"));

        Expect.Equal(scene.Directory, facts.MeasuredPath);
        Expect.True(facts.FreeBytes > 0);
    }

    /// <summary>An existing directory is measured as itself, and a missing one is not walked up from.</summary>
    [Fact]
    public void FreeDiskIfExists_measures_an_existing_directory_and_is_null_for_a_missing_one()
    {
        var scene = Create(out var holder);
        using var _ = holder;

        var present = scene.Probes.FreeDiskIfExists(scene.Directory);
        var absent = scene.Probes.FreeDiskIfExists(Path.Combine(scene.Directory, "absent"));

        Expect.Equal(scene.Directory, present?.MeasuredPath);
        Expect.True(present?.FreeBytes > 0);
        Expect.True(absent is null);
    }

    /// <summary>A bound port is taken, and the fallback is the first free one.</summary>
    [Fact]
    public void A_taken_preferred_port_names_the_free_fallback()
    {
        var scene = Create(out var holder);
        using var _ = holder;
        scene.ReleaseFreePort();

        var facts = scene.Probes.Ports(scene.TakenPort, scene.FreePort, scene.FreePort);

        Expect.Equal(new PortFacts(scene.TakenPort, PreferredFree: false, scene.FreePort), facts);
    }

    /// <summary>A free preferred port is free.</summary>
    [Fact]
    public void A_free_preferred_port_is_free()
    {
        var scene = Create(out var holder);
        using var _ = holder;
        scene.ReleaseFreePort();

        var facts = scene.Probes.Ports(scene.FreePort, scene.TakenRangeFirst, scene.TakenRangeLast);

        Expect.Equal(new PortFacts(scene.FreePort, PreferredFree: true, FirstFreeFallback: null), facts);
    }

    /// <summary>A taken preferred port with every port of the fallback range taken has no fallback: the range itself is probed.</summary>
    [Fact]
    public void A_taken_preferred_port_with_every_fallback_taken_has_no_fallback()
    {
        var scene = Create(out var holder);
        using var _ = holder;

        var facts = scene.Probes.Ports(scene.TakenPort, scene.TakenRangeFirst, scene.TakenRangeLast);

        Expect.True(scene.TakenRangeLast > scene.TakenRangeFirst);
        Expect.Equal(new PortFacts(scene.TakenPort, PreferredFree: false, FirstFreeFallback: null), facts);
    }
}
