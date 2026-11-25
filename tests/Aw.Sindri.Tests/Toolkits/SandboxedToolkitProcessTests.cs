using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>A toolkit process owns its run area (agent-first PR 16): the plan's files go when the process is stopped, restarted after a crash or shut down, never only when the connect failed.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxedToolkitProcessTests
{
    [Fact]
    public async Task Disposing_the_process_ends_it_and_discards_the_run_area_exactly_once()
    {
        var sandbox = new RecordingSandbox();
        var plan = sandbox.PlanProcess(new ProcessSandboxRequest(
            LaunchId.From("LCH-" + new string('e', 32)), "toolkit:triage", SandboxRequests.SindriHome + "/toolkits/triage/bin/triage", "/run/area", [],
            new Dictionary<string, string>(StringComparer.Ordinal), [], new SandboxNetFacts("/run/net", 7480, 41235), SandboxRequests.Host)).Value;
        var inner = new CountingProcess();
        var process = new SandboxedToolkitProcess(inner, sandbox, plan);

        await process.DisposeAsync();
        await process.DisposeAsync();

        Assert.Equal(1, inner.Disposed);
        Assert.Same(plan, Assert.Single(sandbox.Discarded));
    }

    private sealed class CountingProcess : IToolkitProcess
    {
        public Task Exited => Task.CompletedTask;

        internal int Disposed { get; private set; }

        public ValueTask<Result<string>> Call(string tool, string inputJson, TimeSpan timeout, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>("{}");

        public ValueTask DisposeAsync()
        {
            Disposed++;
            return ValueTask.CompletedTask;
        }
    }
}
