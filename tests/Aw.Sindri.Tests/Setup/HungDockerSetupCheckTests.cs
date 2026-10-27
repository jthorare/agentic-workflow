using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Setup;

/// <summary>
/// A Docker that never answers (#1121): the setup route still answers once the probe's budget has passed, with the Onyx step failed and naming Docker, and the other five steps as usual.
/// The runtime here blocks until it is cancelled; the budget is the host's FakeTimeProvider, advanced by the test, never a sleep.
/// </summary>
public sealed class HungDockerSetupCheckTests
{
    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_setup_route_answers_with_the_onyx_step_failed_when_docker_never_answers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, extra: services => services
            .Replace(ServiceDescriptor.Singleton<IContainerRuntime>(new HungRuntime()))
            .Replace(ServiceDescriptor.Singleton<IHostProbes>(new FakeHostProbes())));
        var clock = (FakeTimeProvider)host.App.Services.GetRequiredService<TimeProvider>();

        var asked = host.Http.Ask(Get("/sindri/setup", Human(host.Token.Value)));
        while (!asked.IsCompleted)
        {
            clock.Advance(TimeSpan.FromMinutes(1));   // past any budget the probe has; repeated because the probe may not have started waiting yet
            await Task.Yield();
        }

        var answer = await asked;
        Assert.Equal(System.Net.HttpStatusCode.OK, answer.Status);
        using var document = JsonDocument.Parse(answer.Body);
        var steps = document.RootElement.GetProperty("steps").EnumerateArray().ToList();
        var onyx = Assert.Single(steps, static s => s.GetProperty("id").GetString() == "onyx");
        Assert.Equal(6, steps.Count);
        Assert.Equal("failed", onyx.GetProperty("state").GetString());
        Assert.Contains("Docker", onyx.GetProperty("error").GetProperty("sentence").GetString(), StringComparison.Ordinal);
    }

    // Inspect waits for its token, as a docker CLI that hangs does until the runner kills it; nothing else is asked of a runtime that never answers.
    private sealed class HungRuntime : IContainerRuntime
    {
        public ContainerRuntimeFacts Inspect(CancellationToken cancellationToken)
        {
            _ = cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("the hung runtime was not cancelled");
        }

        public Result<string> ServerPlatform(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Result<Unit> Pull(ComposeProject project, Action<string> progress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Result<Unit> Up(ComposeProject project, Action<string> progress, TimeSpan? limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Result<Unit> Stop(ComposeProject project, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Result<IReadOnlyList<ComposeContainer>> Ps(ComposeProject project, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Result<ComposeConfigFacts> RenderedConfig(ComposeProject project, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Result<IReadOnlyDictionary<string, IReadOnlyList<string>>> RepoDigests(IReadOnlyList<string> images, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Result<long> MemoryUsedOutside(ComposeProject project, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
