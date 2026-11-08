using Aw.Sindri.Core.Launch;
using Xunit;

namespace Aw.Sindri.Testing.Launch;

/// <summary>The contract every <see cref="ILauncher"/> honours: the fake passes it now; the real launcher (U7) is red until it does.</summary>
public abstract class LauncherContract
{
    /// <summary>The launcher under test.</summary>
    /// <returns>The launcher.</returns>
    protected abstract ILauncher Create();

    /// <summary>How many distinct sessions the launcher under test has started and not stopped.</summary>
    /// <returns>The count.</returns>
    protected abstract int SessionCount();

    /// <summary>A second Launch with one launch id returns the first one's Started and starts no second session.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Launch_is_idempotent_per_LaunchId()
    {
        var launcher = Create();
        var request = A.LaunchRequest().Build();

        var first = await launcher.Launch(request, CancellationToken.None);
        var second = await launcher.Launch(request, CancellationToken.None);

        Expect.True(first is StartedLaunch && second is StartedLaunch);
        Expect.Equal(((StartedLaunch)first).Host, ((StartedLaunch)second).Host);
        Expect.Equal(1, SessionCount());
    }

    /// <summary>
    /// A launcher that did not start the session (a Sindri restart: engine P8 re-sends the same id) answers a Launch of the id of a live session with that session and starts no second one.
    /// A launcher whose <see cref="Create"/> returns one object keeps its memory, which is the same promise.
    /// </summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_launcher_answers_the_live_session_of_a_launch_id()
    {
        var request = A.LaunchRequest().Build();
        var first = await Create().Launch(request, CancellationToken.None);

        var again = await Create().Launch(request, CancellationToken.None);

        Expect.True(first is StartedLaunch && again is StartedLaunch);
        Expect.Equal(((StartedLaunch)first).Host, ((StartedLaunch)again).Host);
        Expect.Equal(1, SessionCount());
    }

    /// <summary>Stop is safe to repeat, and for a launch that never started.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Stop_is_idempotent()
    {
        var launcher = Create();
        var request = A.LaunchRequest().Build();
        _ = await launcher.Launch(request, CancellationToken.None);

        var first = await launcher.Stop(request.Launch, CancellationToken.None);
        var second = await launcher.Stop(request.Launch, CancellationToken.None);
        var never = await launcher.Stop(LaunchId.From("LCH-" + new string('0', 32)), CancellationToken.None);

        Expect.True(first.IsSuccess && second.IsSuccess && never.IsSuccess);
        Expect.Equal(0, SessionCount());
    }

    /// <summary>An unattended launch skips the viewer.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unattended_launch_skips_the_viewer()
    {
        var step = new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"));

        var directory = Directory.CreateTempSubdirectory("aw-launcher-contract-");   // an unattended Step makes its output folder in the directory it works in
        try
        {
            var result = await Create().Launch(A.LaunchRequest().InDirectory(directory.FullName).Unattended(step).Build(), CancellationToken.None);

            Expect.True(result is StartedLaunch { Viewer.Kind: ViewerOutcomeKind.Skipped });
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
