using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Scoping;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The candidate packet's hash tool (scoping spec API9): a tool that is missing or fails is a tool error, never "the packet changed".</summary>
public sealed class ScopePacketStoreTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly WorkflowInstanceId _instance = WorkflowInstanceId.From("WFI-" + new string('1', 32));

    public void Dispose() => _home.Dispose();

    private sealed class Runner(ToolRun run) : IToolRunner
    {
        public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default) => run;
    }

    private ScopePacketStore Store(ToolRun run)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "workflow", "runs", _instance.Value, "scope-packet"));
        return new ScopePacketStore(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new AwPacketsTool(new Runner(run), static _ => null));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_aw_packets_is_SND_SCOPING_008_not_007()
    {
        var result = await Store(ToolRun.Missing).Hash(_instance, TestContext.Current.CancellationToken);

        Assert.Equal(ScopingErrors.PacketToolFailed.Code, result.Error.Code);
        Assert.Contains("(fix: ", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_nonzero_exit_is_SND_SCOPING_008_not_007()
    {
        var result = await Store(new ToolRun(ToolOutcome.Exited, 1, string.Empty, "boom")).Hash(_instance, TestContext.Current.CancellationToken);

        Assert.Equal(ScopingErrors.PacketToolFailed.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_timeout_is_SND_SCOPING_008()
    {
        var result = await Store(ToolRun.TimedOut).Hash(_instance, TestContext.Current.CancellationToken);

        Assert.Equal(ScopingErrors.PacketToolFailed.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_hash_on_stdout_is_returned()
    {
        var hash = "sha256:" + new string('a', 64);

        var result = await Store(new ToolRun(ToolOutcome.Exited, 0, hash + "\n")).Hash(_instance, TestContext.Current.CancellationToken);

        Assert.Equal(hash, result.Value);
    }
}
