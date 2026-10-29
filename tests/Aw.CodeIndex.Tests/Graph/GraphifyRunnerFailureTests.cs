using System.Text;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Graph;
using Aw.CodeIndex.Testing;
using Aw.Kernel;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Graph;

/// <summary>The graph layer's detail when graphify ends non-zero says the exit or signal and the end of stderr (#1680).</summary>
[Trait("Lane", "integration")]
public sealed class GraphifyRunnerFailureTests : IDisposable
{
    private const string Pin = "0.9.67";

    private static readonly string[] _paths = ["src/a.ts", "src/b.ts"];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aw-graphify-fail-" + RealTime.NewGuid().ToString("N"));

    public GraphifyRunnerFailureTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private async Task<string> DetailOf(string body)
    {
        var bin = Directory.CreateDirectory(Path.Combine(_directory, "env", "bin")).FullName;
        var tool = ExecutableScript.Write(Path.Combine(bin, "graphify"), $"if [ \"$1\" = \"--version\" ]; then echo graphify {Pin}; exit 0; fi\n{body}\n");
        var runner = new GraphifyRunner(new GraphifyOptions(Pin, tool, Path.Combine(_directory, "tmp")) { Sandbox = static (_, command, _, _) => command }, new FakeBodyScrubber());
        var blobs = new Blobs(_paths.ToDictionary(p => "b-" + p, p => "// " + p, StringComparer.Ordinal));

        var run = await runner.Run(FakeGraphRunnerContractTests.RequestOf(blobs), TestContext.Current.CancellationToken);

        Assert.True(run.IsError);
        return run.Error.Message;
    }

    [Fact]
    public async Task A_failed_run_names_its_exit_code_and_the_last_line_of_stderr()
    {
        var detail = await DetailOf("echo starting >&2; echo 'boom: parser crashed' >&2; exit 3");

        Assert.Equal("failed: graphify exited with code 3: starting | boom: parser crashed", detail);
    }

    [Fact]
    public async Task A_run_killed_by_a_signal_says_which()
    {
        var detail = await DetailOf("echo 'about to die' >&2; kill -9 $$");

        Assert.Equal("failed: graphify was killed by signal 9 (exit 137): about to die", detail);
    }

    [Fact]
    public async Task Stderr_longer_than_the_cap_keeps_only_its_end()
    {
        var detail = await DetailOf("i=0; while [ $i -lt 400 ]; do echo \"line $i of a very long complaint from graphify\" >&2; i=$((i+1)); done; echo THE-END >&2; exit 2");

        Assert.StartsWith("failed: graphify exited with code 2: ", detail, StringComparison.Ordinal);
        Assert.EndsWith("THE-END", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("line 0 ", detail, StringComparison.Ordinal);
        Assert.True(detail.Length <= "failed: graphify exited with code 2: ".Length + GraphifyFailure.MaxCharacters);
    }

    private sealed class Blobs(Dictionary<string, string> contents) : IBlobReader
    {
        public ValueTask<Result<byte[]?>> Read(string blob, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<byte[]?>>(contents.TryGetValue(blob, out var text) ? Encoding.UTF8.GetBytes(text) : null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
