using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The aw-packets adapter: the arguments it passes, the JSON it reads, and the failures it names.</summary>
[Lane(TestLane.Unit)]
public sealed class AwPacketsToolTests
{
    private sealed class Runner(ToolRun run) : IToolRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((fileName, arguments));
            return run;
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (AwPacketsTool Tool, Runner Runner) Tool(ToolRun run, string? program = null)
    {
        var runner = new Runner(run);
        return (new AwPacketsTool(runner, name => string.Equals(name, "AW_PACKETS_BIN", StringComparison.Ordinal) ? program : null), runner);
    }

    [Fact]
    public async Task Validate_passes_the_kind_directory_and_repo_and_reads_the_failures()
    {
        var json = """{"schemaVersion":1,"packet":"p1","passed":false,"checks":[{"rule":"PKT-BASE-001","state":"pass","subject":"packet","message":"ok"},{"rule":"PKT-SCOPE-001","state":"fail","subject":"task t1","message":"matches nothing","fix":"fix the glob"}]}""";
        var (tool, runner) = Tool(new ToolRun(ToolOutcome.Exited, 1, json), "/bin/aw-packets");

        var result = await tool.Validate(new PacketRef(PacketKind.Phase, "/run/packet", Repo: "/work/web"), Token);

        Assert.Equal("/bin/aw-packets", runner.Calls[0].FileName);
        Assert.Equal(["validate", "--kind", "phase", "/run/packet", "--repo", "/work/web", "--json"], runner.Calls[0].Arguments);
        Assert.False(result.Value.Passed);
        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal(new PacketFailure("PKT-SCOPE-001", "task t1", "matches nothing", "fix the glob"), failure);
    }

    [Fact]
    public async Task A_build_packet_takes_no_kind_flag()
    {
        var (tool, runner) = Tool(new ToolRun(ToolOutcome.Exited, 0, """{"passed":true,"checks":[]}"""));

        var result = await tool.Validate(new PacketRef(PacketKind.Build, "/run/packet"), Token);

        Assert.Equal(["validate", "/run/packet", "--json"], runner.Calls[0].Arguments);
        Assert.True(result.Value.Passed);
    }

    [Fact]
    public async Task Freeze_by_owner_reads_the_stored_record()
    {
        var hash = "sha256:" + new string('b', 64);
        var json = $$"""{"frozen":true,"hash":"{{hash}}","by":"machine","alreadyFrozen":true,"alreadyFrozenBy":"machine","checks":[]}""";
        var (tool, runner) = Tool(new ToolRun(ToolOutcome.Exited, 0, json));

        var result = await tool.Freeze(new PacketRef(PacketKind.Scoping, "/run/scope", RepoSlug: "o/n", Instance: "T-1"), PacketFreezer.Owner, Token);

        Assert.Equal(["freeze", "--kind", "scoping", "/run/scope", "--repo-slug", "o/n", "--instance", "T-1", "--by", "owner", "--json"], runner.Calls[0].Arguments);
        var freeze = result.Value;
        Assert.True(freeze.Frozen);
        Assert.Equal(hash, freeze.Hash);
        Assert.Equal(PacketFreezer.Machine, freeze.By);
        Assert.True(freeze.AlreadyFrozen);
        Assert.Equal(PacketFreezer.Machine, freeze.AlreadyFrozenBy);
        Assert.Empty(freeze.Failures);
    }

    [Fact]
    public async Task Hash_reads_the_plain_hash_and_passes_no_json_flag()
    {
        var hash = "sha256:" + new string('a', 64);
        var (tool, runner) = Tool(new ToolRun(ToolOutcome.Exited, 0, hash + "\n"));

        var result = await tool.Hash(new PacketRef(PacketKind.Proof, "/run/proof", Repo: "/ignored"), Token);

        Assert.Equal(["hash", "--kind", "proof", "/run/proof"], runner.Calls[0].Arguments);
        Assert.Equal(hash, result.Value);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(139)]
    public async Task An_exit_other_than_0_or_1_is_a_tool_error_with_a_fix(int exit)
    {
        var (tool, _) = Tool(new ToolRun(ToolOutcome.Exited, exit, string.Empty, "aw-packets: AW-PACKETS-002 no packet"));

        var result = await tool.Validate(new PacketRef(PacketKind.Build, "/nope"), Token);

        Assert.Equal(ScopingErrors.PacketToolFailed.Code, result.Error.Code);
        Assert.Contains("AW-PACKETS-002", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("(fix: ", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_hash_exit_1_is_an_error_not_an_answer()
    {
        var (tool, _) = Tool(new ToolRun(ToolOutcome.Exited, 1, string.Empty, "AW-PACKETS-003"));

        var result = await tool.Hash(new PacketRef(PacketKind.Scoping, "/p"), Token);

        Assert.Equal(ScopingErrors.PacketToolFailed.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(2)]   // ToolOutcome.Missing
    [InlineData(3)]   // ToolOutcome.TimedOut
    public async Task A_missing_or_slow_tool_is_a_tool_error(int outcome)
    {
        var (tool, _) = Tool(new ToolRun((ToolOutcome)outcome, -1, string.Empty));

        var result = await tool.Freeze(new PacketRef(PacketKind.Build, "/p"), PacketFreezer.Machine, Token);

        Assert.Equal(ScopingErrors.PacketToolFailed.Code, result.Error.Code);
    }

    [Fact]
    public async Task Output_that_is_not_json_is_a_tool_error()
    {
        var (tool, _) = Tool(new ToolRun(ToolOutcome.Exited, 0, "PASS"));

        var result = await tool.Validate(new PacketRef(PacketKind.Build, "/p"), Token);

        Assert.Equal(ScopingErrors.PacketToolFailed.Code, result.Error.Code);
    }

    private const string Malformed = """{"code":"AW-PACKETS-003","message":"packet.json repeats the key \"files\""}""";

    [Fact]
    public async Task AMalformed_packet_exit_1_with_no_json_names_the_tools_error_on_validate()
    {
        var (tool, _) = Tool(new ToolRun(ToolOutcome.Exited, 1, string.Empty, Malformed));

        var result = await tool.Validate(new PacketRef(PacketKind.Phase, "/p"), Token);

        AssertNamesTheToolsError(result.Error.Message);
    }

    [Fact]
    public async Task AMalformed_packet_exit_1_with_no_json_names_the_tools_error_on_freeze()
    {
        var (tool, _) = Tool(new ToolRun(ToolOutcome.Exited, 1, string.Empty, Malformed));

        var result = await tool.Freeze(new PacketRef(PacketKind.Phase, "/p"), PacketFreezer.Machine, Token);

        AssertNamesTheToolsError(result.Error.Message);
    }

    private static void AssertNamesTheToolsError(string message)
    {
        Assert.Contains("AW-PACKETS-003", message, StringComparison.Ordinal);
        Assert.DoesNotContain("build aw-packets", message, StringComparison.Ordinal);
        Assert.DoesNotContain("it printed no JSON", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scoping_packet_passes_no_repo_to_validate_or_freeze()
    {
        var (tool, runner) = Tool(new ToolRun(ToolOutcome.Exited, 0, """{"passed":true,"frozen":true,"checks":[]}"""));
        var packet = new PacketRef(PacketKind.Scoping, "/run/scope", Repo: "/work/web", RepoSlug: "o/n", Instance: "T-1");

        _ = await tool.Validate(packet, Token);
        _ = await tool.Freeze(packet, PacketFreezer.Machine, Token);

        Assert.Equal(2, runner.Calls.Count);
        Assert.All(runner.Calls, call => Assert.DoesNotContain(call.Arguments, static argument => string.Equals(argument, "--repo", StringComparison.Ordinal)));
    }
}
