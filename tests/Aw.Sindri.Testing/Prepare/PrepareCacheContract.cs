using Aw.Sindri.Core.Launch;
using Xunit;

namespace Aw.Sindri.Testing.Prepare;

/// <summary>
/// The contract every <see cref="IPrepareCache"/> honours. The scenario gives a source package directory that holds an install, and an empty target package directory.
/// </summary>
public abstract class PrepareCacheContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The port and the scenario.</summary>
    /// <returns>The port, the directory an install just made, the directory a hit is copied into, and whether a directory now holds the install.</returns>
    protected abstract ValueTask<(IPrepareCache Port, string Source, string Target, Func<string, bool> Installed)> Scenario();

    /// <summary>A key nobody stored is a miss.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_key_is_a_miss()
    {
        var (port, _, _, _) = await Scenario();

        Expect.True(!port.Has("never-stored"));
    }

    /// <summary>A store makes the key a hit.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_stored_key_is_a_hit()
    {
        var (port, source, _, _) = await Scenario();

        var stored = await port.Store("key-1", source, Token);

        Expect.True(stored.IsSuccess && port.Has("key-1"));
        Expect.True(!port.Has("key-2"));
    }

    /// <summary>A hit puts the install in the target.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_hit_copies_the_install_into_the_target()
    {
        var (port, source, target, installed) = await Scenario();
        _ = await port.Store("key-1", source, Token);

        var restored = await port.Restore("key-1", target, Token);

        Expect.True(restored.IsSuccess && installed(target));
    }
}
