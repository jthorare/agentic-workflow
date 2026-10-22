using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Graph;
using Aw.CodeIndex.Testing;
using Aw.Kernel;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Graph;

/// <summary>The real runner over a stand-in graphify (a shell script that prints the pin and writes a fixed graph.json) with a pass-through sandbox, so the run path needs no bubblewrap.</summary>
[Trait("Lane", "integration")]
public sealed class GraphifyRunnerContractTests : GraphRunnerContract, IDisposable
{
    private const string Pin = "0.9.67";

    private const string GraphJson =
        "{\"directed\":false,\"nodes\":["
        + "{\"id\":\"src_a_f\",\"label\":\"f()\",\"source_file\":\"src/a.ts\",\"source_location\":\"L1\",\"_callable\":true},"
        + "{\"id\":\"src_b_g\",\"label\":\"g()\",\"source_file\":\"src/b.ts\",\"source_location\":\"L2\",\"_callable\":true},"
        + "{\"id\":\"src_x_h\",\"label\":\"h()\",\"source_file\":\"src/x.ts\",\"source_location\":\"L3\",\"_callable\":true}],"
        + "\"links\":["
        + "{\"source\":\"src_a_f\",\"target\":\"src_b_g\",\"relation\":\"calls\",\"confidence\":\"EXTRACTED\",\"source_file\":\"src/a.ts\",\"source_location\":\"L4\"},"
        + "{\"source\":\"src_a_f\",\"target\":\"src_x_h\",\"relation\":\"calls\",\"confidence\":\"EXTRACTED\",\"source_file\":\"src/a.ts\",\"source_location\":\"L5\"}]}";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aw-graphify-" + RealTime.NewGuid().ToString("N"));

    public GraphifyRunnerContractTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Tool(string version, string body)
    {
        var bin = Directory.CreateDirectory(Path.Combine(_directory, "env", "bin")).FullName;
        var path = Path.Combine(bin, "graphify");
        File.WriteAllText(path, $"#!/bin/sh\nif [ \"$1\" = \"--version\" ]; then echo graphify {version}; exit 0; fi\n{body}\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherExecute);
        }

        return path;
    }

    private string Writes(string json)
    {
        var graph = Path.Combine(_directory, "emitted.json");
        File.WriteAllText(graph, json);
        return $"mkdir -p \"$2/graphify-out\" && cp '{graph}' \"$2/graphify-out/graph.json\"";
    }

    private GraphifyRunner Runner(string binary, Func<GraphHost, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>?>? sandbox = null)
        => new(new GraphifyOptions(Pin, binary, Path.Combine(_directory, "tmp")) { Sandbox = sandbox ?? ((_, command, _, _) => command) }, new FakeBodyScrubber());

    private static GraphRequest Request(Dictionary<string, string>? contents = null)
    {
        var blobs = new Blobs(contents ?? RequestPaths.ToDictionary(p => "b-" + p, p => "// " + p, StringComparer.Ordinal));
        return FakeGraphRunnerContractTests.RequestOf(blobs);
    }

    protected override (IGraphRunner Runner, GraphRequest Request) ArrangeWithOutsider() => (Runner(Tool(Pin, Writes(GraphJson))), Request());

    protected override (IGraphRunner Runner, GraphRequest Request) ArrangeMissingTool() => (Runner(Path.Combine(_directory, "nope", "bin", "graphify")), Request());

    [Fact]
    public async Task A_version_off_the_pin_is_the_pin_code_and_the_tool_never_extracts()
    {
        var marker = Path.Combine(_directory, "extracted");
        var runner = Runner(Tool("0.9.99", $"touch '{marker}'"));

        var run = await runner.Run(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(CodeIndexErrors.PinMismatch.Code, run.Error.Code);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task No_sandbox_is_the_sandbox_code_and_the_tool_never_runs()
    {
        var runner = Runner(Tool(Pin, Writes(GraphJson)), sandbox: (_, _, _, _) => null);

        var probe = await runner.Probe(TestContext.Current.CancellationToken);

        Assert.Equal(CodeIndexErrors.NoSandbox.Code, probe.Error.Code);
    }

    [Fact]
    public async Task The_snapshot_holds_the_request_files_and_nothing_else_and_is_gone_after_the_run()
    {
        var listing = Path.Combine(_directory, "listing");
        var runner = Runner(Tool(Pin, $"(cd \"$2\" && find . -type f | sort) > '{listing}'\n{Writes(GraphJson)}"));
        var contents = RequestPaths.ToDictionary(p => "b-" + p, p => "// " + p, StringComparer.Ordinal);
        foreach (var extra in new[] { "b-x", "b-g", "b-e" })
        {
            contents[extra] = "must not be written";
        }

        var request = Request(contents) with { Files = [.. RequestPaths.Select(p => new SnapshotFile(p, "b-" + p, 10)), new SnapshotFile("graphify-out/x.json", "b-x", 1), new SnapshotFile(".git/config", "b-g", 1), new SnapshotFile("a/../../escape.ts", "b-e", 1)] };

        _ = await runner.Run(request, TestContext.Current.CancellationToken);

        Assert.Equal(["./src/a.ts", "./src/b.ts"], await File.ReadAllLinesAsync(listing, TestContext.Current.CancellationToken), StringComparer.Ordinal);
        Assert.Empty(Directory.GetDirectories(Path.Combine(_directory, "tmp"), "run-*"));
        Assert.False(File.Exists(Path.Combine(_directory, "escape.ts")));
    }

    [Fact]
    public async Task A_tool_that_fails_or_writes_no_graph_is_the_graph_code_with_its_detail()
    {
        var failed = await Runner(Tool(Pin, "exit 3")).Run(Request(), TestContext.Current.CancellationToken);
        var silent = await Runner(Tool(Pin, "exit 0")).Run(Request(), TestContext.Current.CancellationToken);
        var wrong = await Runner(Tool(Pin, Writes("[]"))).Run(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(("SND-CODEIDX-008", "failed"), (failed.Error.Code, failed.Error.Message));
        Assert.Equal(("SND-CODEIDX-008", "shape"), (silent.Error.Code, silent.Error.Message));
        Assert.Equal(("SND-CODEIDX-008", "shape"), (wrong.Error.Code, wrong.Error.Message));
    }

    [Fact]
    public async Task A_graph_over_the_size_cap_is_too_large()
    {
        var runner = Runner(Tool(Pin, Writes(GraphJson)));
        var request = Request() with { Settings = new GraphSettings(Enabled: true, MaxMegabytes: 0, MaxNodes: 1_000) };

        var run = await runner.Run(request, TestContext.Current.CancellationToken);

        Assert.Equal("too-large", run.Error.Message);
    }

    [Fact]
    public void The_shipped_sandbox_binds_the_environment_read_only_after_the_credential_mask_and_the_snapshot_writable()
    {
        var host = new GraphHost(IsLinux: true, IsMac: false, "/home/u", RuntimeDir: null, _ => true, p => p == "/home/u/.agentic-workflow" ? PathKind.Directory : PathKind.Missing, p => p);

        var argv = GraphSandbox.Argv(host, ["/env/bin/graphify", "extract"], ["/snap"], ["/home/u/.agentic-workflow/cache/env"])!;

        var mask = argv.ToList().IndexOf("/home/u/.agentic-workflow");
        var readOnly = argv.ToList().IndexOf("/home/u/.agentic-workflow/cache/env");
        Assert.True(mask >= 0 && readOnly > mask, "the --ro-bind of the environment comes after the --tmpfs mask");
        Assert.Equal("--ro-bind", argv[readOnly - 1]);
        Assert.Contains("--unshare-all", argv, StringComparer.Ordinal);
        Assert.Equal("--bind", argv[argv.ToList().IndexOf("/snap") - 1]);
    }

    [Fact]
    public void The_macos_profile_allows_the_environment_after_the_denial()
    {
        var host = new GraphHost(IsLinux: false, IsMac: true, "/Users/u", RuntimeDir: null, _ => true, _ => PathKind.Missing, p => p);

        var profile = GraphSandbox.Argv(host, ["g"], ["/snap"], ["/Users/u/.agentic-workflow/cache/env"])![2];

        Assert.True(profile.IndexOf("(deny file-read*", StringComparison.Ordinal) < profile.IndexOf("(allow file-read* (subpath \"/Users/u/.agentic-workflow/cache/env\"))", StringComparison.Ordinal));
        Assert.Contains("(deny network*)", profile, StringComparison.Ordinal);
    }

    private sealed class Blobs(Dictionary<string, string> contents) : IBlobReader
    {
        public ValueTask<Result<byte[]?>> Read(string blob, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<byte[]?>>(contents.TryGetValue(blob, out var text) ? Encoding.UTF8.GetBytes(text) : null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
