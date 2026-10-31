using System.Diagnostics;
using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The sandbox shims (spec §3.3 rule 2): the generated scripts run against a fake heavy and fake tools, and the table matches its golden and the real heavy.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class SandboxShimsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-shims-" + RealTime.NewGuid().ToString("N"));

    public SandboxShimsTests()
    {
        Directory.CreateDirectory(_dir);
        foreach (var tool in SandboxShims.Tools)
        {
            Script("real-" + tool, $"echo \"tool {tool} $*\"");
        }

        Script("heavy", "echo \"heavy $*\"");
        SandboxShims.Write(Path.Combine(_dir, "shims"), SandboxShims.Tools.ToDictionary(t => t, t => Path.Combine(_dir, "real-" + t), StringComparer.Ordinal), Path.Combine(_dir, "heavy"));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory, Trait("Lane", "unit")]
    [InlineData("dotnet", "build X.csproj", "expensive")]
    [InlineData("dotnet", "publish", "expensive")]
    [InlineData("dotnet", "format a.cs", "expensive")]
    [InlineData("dotnet", "pack", "expensive")]
    [InlineData("dotnet", "restore", "expensive")]
    [InlineData("dotnet", "run --project X", "expensive")]
    [InlineData("dotnet", "test X.csproj", "expensive")]
    [InlineData("dotnet", "test --no-build X.csproj", "light")]
    [InlineData("dotnet", "test X.csproj --no-build", "light")]
    [InlineData("dotnet", "-v q test X.csproj", "expensive")]
    [InlineData("dotnet", "new classlib", "light")]
    [InlineData("dotnet", "sln list", "light")]
    [InlineData("dotnet", "test --no-build AgenticWorkflow.slnx", "expensive")]
    [InlineData("dotnet", "--version", "direct")]
    [InlineData("dotnet", "--info", "direct")]
    [InlineData("dotnet", "--list-sdks", "direct")]
    [InlineData("dotnet", "--list-runtimes", "direct")]
    [InlineData("npm", "ci", "expensive")]
    [InlineData("npm", "install left-pad", "expensive")]
    [InlineData("npm", "test", "expensive")]
    [InlineData("npm", "run lint", "light")]
    [InlineData("npm", "ls", "light")]
    [InlineData("npm", "view x", "light")]
    [InlineData("npm", "run vitest", "expensive")]
    [InlineData("npm", "--version", "direct")]
    [InlineData("npx", "vitest run", "expensive")]
    [InlineData("npx", "tsc -p .", "expensive")]
    [InlineData("npx", "playwright test", "expensive")]
    [InlineData("npx", "prettier x", "light")]
    [InlineData("npm", "--prefix sub ci", "expensive")]
    [InlineData("npx", "-p typescript tsc", "expensive")]
    [InlineData("npm", "-w pkg run lint", "light")]
    [InlineData("dotnet", "run --project src/x/aot-smoke", "expensive")]
    [InlineData("dotnet", "exec ui-evidence.dll", "expensive")]
    [InlineData("dotnet", "exec Aw.Evidence.dll", "expensive")]
    [InlineData("dotnet", "msbuild e2e.sh x", "expensive")]
    public void Shim_routes_each_row(string tool, string args, string route)
    {
        var (output, code) = Run(tool, args, slotHeld: false);
        Assert.Equal(0, code);
        var real = Path.Combine(_dir, "real-" + tool);
        var expected = route == "direct" ? $"tool {tool} {args}" : $"heavy --{route} {real} {args}";
        Assert.Equal(expected, output);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("dotnet", "build X.csproj")]
    [InlineData("dotnet", "test --no-build X")]
    [InlineData("npm", "ci")]
    [InlineData("npx", "vitest")]
    [InlineData("npx", "playwright test")]
    public void A_held_slot_runs_the_real_tool_directly(string tool, string args)
    {
        var (output, code) = Run(tool, args, slotHeld: true);
        Assert.Equal(0, code);
        Assert.Equal($"tool {tool} {args}", output);
    }

    // #1811: a child another thread forks during SandboxShims.Write holds the shim's write descriptor until it execs, and an exec of the shim in that moment fails with ETXTBSY
    // ('text file busy', sh exit 126). The test holds a write descriptor itself for that moment, so the race is certain instead of rare.
    [Fact, Trait("Lane", "unit")]
    public async Task A_shim_starts_after_a_moment_of_text_file_busy()
    {
        var shim = Path.Combine(_dir, "shims", "dotnet");
        var held = new FileStream(shim, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        var release = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
            await held.DisposeAsync();
        }, TestContext.Current.CancellationToken);

        var (output, code) = Run("dotnet", "--version", slotHeld: false);
        await release;
        Assert.Equal(0, code);
        Assert.Equal("tool dotnet --version", output);
    }

    [Fact, Trait("Lane", "unit")]
    public void Paths_with_quotes_and_spaces_survive()
    {
        var script = SandboxShims.Script("npm", "/a b/it's/npm", "/h h/heavy");
        Assert.Contains("real='/a b/it'\\''s/npm'", script, StringComparison.Ordinal);
        Assert.Contains("heavy='/h h/heavy'", script, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Shim_files_are_executable_and_named_for_the_tool()
    {
        foreach (var tool in SandboxShims.Tools)
        {
            var path = Path.Combine(_dir, "shims", tool);
            Assert.True(File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Table_matches_the_golden()
    {
        var shims = SandboxGoldens.Object("shims.json")["shims"]!.AsArray();
        foreach (var tool in SandboxShims.Tools)
        {
            var expensive = shims.Where(s => (string)s!["shim"]! == tool && (string)s["route"]! == "expensive" && s["unlessFlag"] is null).SelectMany(s => s!["subcommands"]!.AsArray().Select(x => (string)x!)).ToList();
            var direct = shims.Where(s => (string)s!["shim"]! == tool && (string)s["route"]! == "direct").SelectMany(s => s!["subcommands"]!.AsArray().Select(x => (string)x!)).ToList();
            Assert.Equal(expensive.Order(StringComparer.Ordinal), SandboxShims.ExpensiveSubcommands(tool).Order(StringComparer.Ordinal));
            Assert.Equal(direct.Order(StringComparer.Ordinal), SandboxShims.DirectFlags(tool).Order(StringComparer.Ordinal));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Shim_table_matches_heavy_cases()
    {
        var heavy = Environment.GetEnvironmentVariable("AW_HEAVY_BIN") is { Length: > 0 } set
            ? set
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow", "bin-r1b", "heavy");
        Assert.SkipUnless(File.Exists(heavy), "no heavy wrapper at " + heavy);

        var lines = File.ReadAllLines(heavy);
        var patterns = lines
            .Where(l => l.TrimStart().StartsWith('*') && l.Contains(") mode=expensive", StringComparison.Ordinal) && !l.Contains("--no-build", StringComparison.Ordinal))
            .SelectMany(l => l[..l.IndexOf(") mode=expensive", StringComparison.Ordinal)].Trim().Split('|'))
            .Select(p => p.Trim('*').Replace("\"", string.Empty, StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(patterns);

        var heavyAnywhere = patterns.Where(p => !p.Trim().Contains(' ', StringComparison.Ordinal)).ToList();
        var heavyTools = patterns.Except(heavyAnywhere, StringComparer.Ordinal).Select(p => p.Trim().Split(" ", StringSplitOptions.None)).ToList();
        Assert.Equal(SandboxShims.AnywhereMarkers.Order(StringComparer.Ordinal), heavyAnywhere.Order(StringComparer.Ordinal));

        // heavy's `dotnet test` is expensive only without --no-build (its nested case); the shim has its own branch for it.
        Assert.Contains(lines, l => l.Contains("*\" dotnet test \"*)", StringComparison.Ordinal) && l.Contains("--no-build", StringComparison.Ordinal));
        foreach (var tool in SandboxShims.Tools)
        {
            var fromHeavy = heavyTools.Where(t => t[0] == tool).Select(t => t[1]);
            Assert.Equal(SandboxShims.ExpensiveSubcommands(tool).Order(StringComparer.Ordinal), fromHeavy.Order(StringComparer.Ordinal));
        }
    }

    private void Script(string name, string body) => ExecutableScript.Write(Path.Combine(_dir, name), body + "\n");

    private (string Output, int Code) Run(string tool, string args, bool slotHeld)
    {
        var start = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add($"exec '{Path.Combine(_dir, "shims", tool)}' {args}");
        _ = start.Environment.Remove("AW_SANDBOX_SLOT_HELD");
        if (slotHeld)
        {
            start.Environment["AW_SANDBOX_SLOT_HELD"] = "1";
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd().TrimEnd('\n');
        process.WaitForExit();
        return (output, process.ExitCode);
    }
}
