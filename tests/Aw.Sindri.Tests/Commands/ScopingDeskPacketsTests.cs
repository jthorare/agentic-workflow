using System.Diagnostics;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// 'sindri scoping desk evidence' through the CLI with aw-packets behind it (AW_PACKETS_BIN): the hash call and scopePacket.json against a stub that
/// records its arguments, and, against the real built aw-packets, the SC-K proof "aw-packets hash, validate and freeze accept the gold packet".
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class ScopingDeskPacketsTests : IDisposable
{
    private const string StubHash = "sha256:00000000000000000000000000000000000000000000000000000000000000ab";

    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private string? _packets;

    public ScopingDeskPacketsTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string Facts => Path.Combine(_home.Path, "facts");

    private string OutDirectory => Path.Combine(_home.Path, "run");

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, "AW_PACKETS_BIN" => _packets, _ => null };

    private async Task PutGoldFacts()
    {
        _ = Directory.CreateDirectory(Facts);
        foreach (var (key, text) in ScopingFacts.Expiry())
        {
            await File.WriteAllTextAsync(Path.Combine(Facts, key + ".json"), text, TestContext.Current.CancellationToken);
        }
    }

    private Task<int> Evidence() => CommandTable.Run(["scoping", "desk", "evidence", "--facts", Facts, "--out", OutDirectory], Environment, TimeProvider.System);

    private string Script(string name, string body)
    {
        var path = Path.Combine(_home.Path, name);
        return ExecutableScript.Write(path, body + "\n");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Evidence_asks_aw_packets_for_the_hash_of_the_packet_it_wrote_and_records_it_as_scopePacket()
    {
        var argv = Path.Combine(_home.Path, "argv.txt");
        _packets = Script("aw-packets-stub", $"printf '%s\\n' \"$@\" > '{argv}'\necho '{StubHash}'");
        await PutGoldFacts();

        Assert.Equal(0, await Evidence());

        Assert.Equal(["hash", "--kind", "scoping", Path.Combine(OutDirectory, "scope-packet")], await File.ReadAllLinesAsync(argv, TestContext.Current.CancellationToken));
        using var packet = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "scopePacket.json"), TestContext.Current.CancellationToken));
        Assert.Equal(StubHash, packet.RootElement.GetProperty("hash").GetString());
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "report.json"), TestContext.Current.CancellationToken));
        Assert.Equal("scopePacket.json", report.RootElement.GetProperty("outputs").GetProperty("fact:scopePacket").GetString());
        Assert.Equal(14, Directory.GetFiles(Path.Combine(OutDirectory, "scope-packet")).Length);   // ScopePacketFiles.Names: v12 adds relevance.json and gap-search.json
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_aw_packets_that_is_not_there_is_exit_2_with_the_fix_line()
    {
        _packets = Path.Combine(_home.Path, "no-such-aw-packets");
        await PutGoldFacts();

        Assert.Equal(2, await Evidence());

        Assert.Contains("set AW_PACKETS_BIN", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(OutDirectory, "scopePacket.json")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_aw_packets_that_fails_is_exit_2_naming_its_exit_code()
    {
        _packets = Script("aw-packets-broken", "echo 'rejected' >&2\nexit 3");
        await PutGoldFacts();

        Assert.Equal(2, await Evidence());

        Assert.Contains("exit 3: rejected", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "heavy")]   // builds aw-packets, then runs the real tool
    public async Task Aw_packets_hash_validate_and_freeze_accept_the_packet_the_gold_facts_build()
    {
        var dll = await BuildAwPackets();
        _packets = Script("aw-packets", $"exec dotnet '{dll}' \"$@\"");
        await PutGoldFacts();

        Assert.Equal(0, await Evidence());

        var packet = Path.Combine(OutDirectory, "scope-packet");
        using var written = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "scopePacket.json"), TestContext.Current.CancellationToken));
        var hash = written.RootElement.GetProperty("hash").GetString();
        Assert.Matches("^sha256:[0-9a-f]{64}$", hash);
        var again = await RunPackets(dll, "hash", "--kind", "scoping", packet);
        Assert.Equal(0, again.ExitCode);
        Assert.Equal(hash, again.Output.Trim());
        Assert.Equal(0, (await RunPackets(dll, "validate", "--kind", "scoping", packet)).ExitCode);
        var state = Path.Combine(_home.Path, "state");   // freeze writes under AW_STATE_DIR, a temp one here
        var freeze = await RunPackets(dll, ["freeze", "--kind", "scoping", packet, "--repo-slug", "acme/notes", "--instance", "expiry", "--by", "owner"], state);
        Assert.Equal(0, freeze.ExitCode);
        var frozen = Directory.EnumerateFiles(Path.Combine(state, "packets", "acme", "notes", "expiry", "frozen"), "*", SearchOption.AllDirectories).Select(File.ReadAllText);
        Assert.Contains(frozen, text => text.Contains(hash!, StringComparison.Ordinal));
    }

    private Task<(int ExitCode, string Output, string Error)> RunPackets(string dll, params string[] args) => RunPackets(dll, args, Path.Combine(_home.Path, "state"));

    private static async Task<(int ExitCode, string Output, string Error)> RunPackets(string dll, string[] args, string state)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(dll);
        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["AW_STATE_DIR"] = state;
        using var process = Process.Start(start)!;   // why: the program is dotnet, which starts
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await output, await error);
    }

    private static async Task<string> BuildAwPackets()
    {
        var root = RepoRoot();
        var project = Path.Combine(root, "src", "Packets", "Aw.Packets.App", "Aw.Packets.App.csproj");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "build", project, "--nologo", "-v", "q" })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;   // why: the program is dotnet, which starts
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        _ = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, await output);
        return Directory.GetFiles(Path.Combine(root, "artifacts", "bin", "Aw.Packets.App"), "aw-packets.dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
