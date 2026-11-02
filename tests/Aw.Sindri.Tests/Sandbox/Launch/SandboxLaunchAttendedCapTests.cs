using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Sandbox;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The session cap for a session the owner opens (sandbox spec §3.9): a full cap is refused at once, in the attended words; a Step keeps the wait signal and its own words.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxLaunchAttendedCapTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-attended-cap-").FullName;
    private readonly FakeSliceScopes _scopes = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private SandboxLaunch Make(Func<SandboxRequest> request)
    {
        foreach (var unit in new[] { "a", "b", "c" })
        {
            _scopes.Add($"sindri-{unit}.scope");   // the default cap is 3
        }

        return new SandboxLaunch(
            new PassThroughSandbox(), new FileSessionCap(Path.Combine(_dir, "sessions.lock"), _scopes), new FixedStages(request),
            new SandboxLaunchOptions("systemd-run", new BoxFacts(32L * 1024 * 1024 * 1024, 8), _dir, _dir, static () => null));
    }

    [Fact]
    public async Task A_full_cap_refuses_an_attended_open_at_once_in_the_attended_words()
    {
        var launch = Make(static () => Req.Attended);

        var result = await launch.Prepare(A.LaunchRequest().Build(), ["claude"], chosenSession: null, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchSessionWait.Code, result.Error.Code);
        Assert.Contains("No free session slot (`3` of `3` running). Close one, then open again.", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(SandboxReasons.Sessions, SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact]
    public async Task A_full_cap_still_tells_a_Step_to_wait()
    {
        var launch = Make(static () => Req.WriteScope);
        var step = A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))).OnInstance(InstanceAddress.From("ITM-1/spec/1"), RunToken.For(1, 1)).Build();   // A.LaunchRequest() alone is an attended open

        var result = await launch.Prepare(step, ["claude"], chosenSession: null, TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.LaunchSessionWait.Code, result.Error.Code);
        Assert.DoesNotContain("Close one", result.Error.Message, StringComparison.Ordinal);
    }

    private sealed class FixedStages(Func<SandboxRequest> request) : ISandboxLaunchStages
    {
        public ValueTask<Result<SandboxRequest>> Request(LaunchRequest launch, SessionId? chosenSession, CancellationToken cancellationToken) => ValueTask.FromResult<Result<SandboxRequest>>(request());

        public ValueTask<Result<PolicyFiles>> WritePolicy(SandboxRequest request, SandboxPlan plan, string sessionHome, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
