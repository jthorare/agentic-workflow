using Aw.Sindri.Core;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Xunit;

namespace Aw.Sindri.Testing.Launch;

/// <summary>The contract every <see cref="ILaunchFiles"/> honours, run against the fake and the disk adapter: what Verify accepts and what Forget ends.</summary>
public abstract class LaunchFilesContract
{
    /// <summary>The files under test.</summary>
    /// <returns>The files.</returns>
    protected abstract ILaunchFiles Create();

    /// <summary>Makes <paramref name="launch"/> an open launch whose proof is <paramref name="proof"/>.</summary>
    /// <param name="files">The files from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The proof.</param>
    protected abstract void Open(ILaunchFiles files, LaunchId launch, string proof);

    /// <summary>The proof of an open launch is accepted.</summary>
    [Fact]
    public void Verify_accepts_the_proof_of_an_open_launch()
    {
        var files = Create();
        Open(files, A.FirstLaunchId, "p-1");

        Expect.True(files.Verify(A.FirstLaunchId, new LaunchSecret("p-1")).IsSuccess);
    }

    /// <summary>Another proof, an empty one and a launch that was never opened are SND-AUTH-003.</summary>
    [Fact]
    public void Verify_refuses_a_wrong_empty_or_unopened_proof_with_AUTH_003()
    {
        var files = Create();
        Open(files, A.FirstLaunchId, "p-1");
        var other = LaunchId.From("LCH-00000000000000000000000000000002");

        var results = new[] { files.Verify(A.FirstLaunchId, new LaunchSecret("p-2")), files.Verify(A.FirstLaunchId, new LaunchSecret(string.Empty)), files.Verify(other, new LaunchSecret("p-1")) };

        Expect.True(results.All(static r => r.IsError && string.Equals(r.Error.Code, SindriErrors.AuthLaunchSecret.Code, StringComparison.Ordinal)));
    }

    /// <summary>After Forget the launch's proof no longer validates, and forgetting twice is fine.</summary>
    [Fact]
    public void Forget_ends_the_launch_and_is_idempotent()
    {
        var files = Create();
        Open(files, A.FirstLaunchId, "p-1");

        files.Forget(A.FirstLaunchId);
        files.Forget(A.FirstLaunchId);

        Expect.True(files.Verify(A.FirstLaunchId, new LaunchSecret("p-1")).IsError);
    }

    /// <summary>A re-prompt that was recorded reads back with its time, and Forget clears it.</summary>
    [Fact]
    public void MarkNudged_is_read_back_by_NudgedAt_until_the_launch_is_forgotten()
    {
        var files = Create();
        var at = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        Expect.True(files.NudgedAt(A.FirstLaunchId) is null);

        files.MarkNudged(A.FirstLaunchId, at);
        var recorded = files.NudgedAt(A.FirstLaunchId);
        files.Forget(A.FirstLaunchId);

        Expect.Equal((at, true), (recorded!.Value, files.NudgedAt(A.FirstLaunchId) is null));
    }
}
