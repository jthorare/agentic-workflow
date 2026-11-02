using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>What every <see cref="IRunSuspensions"/> does (sandbox spec §7, SB16).</summary>
public abstract class RunSuspensionsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Makes the subject.</summary>
    /// <returns>The subject.</returns>
    protected abstract ValueTask<IRunSuspensions> Create();

    /// <summary>Opens an agent run for <paramref name="launch"/> and leaves it running.</summary>
    /// <param name="subject">The subject from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask Run(IRunSuspensions subject, LaunchId launch);

    /// <summary>Suspends the run of <paramref name="launch"/> (a park or a takeover).</summary>
    /// <param name="subject">The subject from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask Suspend(IRunSuspensions subject, LaunchId launch);

    /// <summary>Resumes the run of <paramref name="launch"/> (an answer or a handback).</summary>
    /// <param name="subject">The subject from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask Resume(IRunSuspensions subject, LaunchId launch);

    /// <summary>A launch nothing knows about is not suspended.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_launch_is_not_suspended()
    {
        var subject = await Create();

        var answer = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(answer.IsSuccess && !answer.Value);
    }

    /// <summary>A running run is not suspended; a suspended one is until it is resumed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_run_is_suspended_from_its_park_or_takeover_until_the_answer_or_handback()
    {
        var subject = await Create();
        await Run(subject, A.FirstLaunchId);
        var running = await subject.IsSuspended(A.FirstLaunchId, Token);
        await Suspend(subject, A.FirstLaunchId);
        var suspended = await subject.IsSuspended(A.FirstLaunchId, Token);
        await Resume(subject, A.FirstLaunchId);
        var resumed = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(running.IsSuccess && !running.Value);
        Expect.True(suspended.IsSuccess && suspended.Value);
        Expect.True(resumed.IsSuccess && !resumed.Value);
    }
}
