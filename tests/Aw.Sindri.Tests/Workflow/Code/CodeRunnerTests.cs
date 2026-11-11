using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow.Code;

namespace Aw.Sindri.Tests.Workflow.Code;

/// <summary>The sindri-code-runner process (agent-first PR 9c), run as the built binary without a sandbox: the timeout kill and the result path.</summary>
[Lane(TestLane.Integration)]
public sealed class CodeRunnerTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aw-code-runner-");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact(Timeout = 60_000)]
    public async Task A_loop_past_the_timeout_is_killed_and_the_node_fails_with_timeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = new CompiledCodeCache(new CodeCompiler(), _directory.FullName).Resolve("while (true) { }", TestContext.Current.CancellationToken);
        var runner = new CodeRunnerProcess(BuiltRunner());

        var result = await runner.Run(code.AssemblyPath!, "null", new CodeLimits(TimeSpan.FromSeconds(1), MemoryMb: 256), ct);

        Assert.Equal("SND-CODE-002", result.Error.Code);
    }

    [Fact(Timeout = 60_000)]
    public async Task The_result_comes_back_as_json()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = new CompiledCodeCache(new CodeCompiler(), _directory.FullName).Resolve("return input![\"items\"]!.AsArray().Select(static i => i!.GetValue<int>() * 2).ToArray();", TestContext.Current.CancellationToken);
        var runner = new CodeRunnerProcess(BuiltRunner());

        var result = await runner.Run(code.AssemblyPath!, "{\"items\":[1,2,3]}", new CodeLimits(TimeSpan.FromSeconds(30), MemoryMb: 256), ct);

        Assert.Equal("[2,4,6]", result.Value);
    }

    [Fact(Timeout = 60_000)]
    public async Task Code_that_throws_fails_the_node_with_the_exception_message()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = new CompiledCodeCache(new CodeCompiler(), _directory.FullName).Resolve("throw new InvalidOperationException(\"boom\");", TestContext.Current.CancellationToken);
        var runner = new CodeRunnerProcess(BuiltRunner());

        var result = await runner.Run(code.AssemblyPath!, "null", new CodeLimits(TimeSpan.FromSeconds(30), MemoryMb: 256), ct);

        Assert.Equal("SND-CODE-004", result.Error.Code);
        Assert.Contains("boom", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task Code_that_exits_before_writing_a_result_fails_the_node()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = new CompiledCodeCache(new CodeCompiler(), _directory.FullName).Resolve("Environment.Exit(0);\nreturn 1;", TestContext.Current.CancellationToken);
        var runner = new CodeRunnerProcess(BuiltRunner());

        var result = await runner.Run(code.AssemblyPath!, "null", new CodeLimits(TimeSpan.FromSeconds(30), MemoryMb: 256), ct);

        Assert.Equal("SND-CODE-004", result.Error.Code);
    }

    [Fact(Timeout = 60_000)]
    public async Task What_the_code_writes_to_the_console_does_not_reach_the_result()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = new CompiledCodeCache(new CodeCompiler(), _directory.FullName).Resolve("Console.Write(\"noise\");\nreturn 1;", TestContext.Current.CancellationToken);
        var runner = new CodeRunnerProcess(BuiltRunner());

        var result = await runner.Run(code.AssemblyPath!, "null", new CodeLimits(TimeSpan.FromSeconds(30), MemoryMb: 256), ct);

        Assert.Equal("1", result.Value);
    }

    // The build-order reference of Aw.Sindri.Tests builds the runner into artifacts/bin/Aw.Sindri.CodeRunner/<configuration>/.
    private static string[] BuiltRunner()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var bin = Path.Combine(dir.FullName, "artifacts", "bin", "Aw.Sindri.CodeRunner");
            if (Directory.Exists(bin))
            {
                var dll = Directory.GetFiles(bin, "sindri-code-runner.dll", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                return dll is null ? throw new FileNotFoundException("sindri-code-runner.dll is not built under " + bin) : ["dotnet", dll];
            }
        }

        throw new DirectoryNotFoundException("no artifacts/bin/Aw.Sindri.CodeRunner above " + AppContext.BaseDirectory);
    }
}
