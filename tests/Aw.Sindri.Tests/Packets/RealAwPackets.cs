using System.Diagnostics;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Packets;

/// <summary>
/// The real built aw-packets (src/Packets/Aw.Packets.App, built by the solution build) behind an <see cref="AwPacketsTool"/>, on a temp home: its AW_STATE_DIR is under the home
/// (freezes write there), its gold design packet copies come from the line fixtures, and its history repo is the scoping fixture's.
/// </summary>
internal sealed class RealAwPackets : IDisposable
{
    private readonly TempHome _home = new();
    private string? _history;

    public string HomePath => _home.Path;

    public void Dispose() => _home.Dispose();

    public AwPacketsTool Tool()
    {
        var wrapper = Path.Combine(_home.Path, "aw-packets");
        File.WriteAllText(wrapper, $"#!/bin/sh\nAW_STATE_DIR='{Path.Combine(_home.Path, "state")}'\nexport AW_STATE_DIR\nexec dotnet '{BuiltDll()}' \"$@\"\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return new AwPacketsTool(new ProcessToolRunner(), name => string.Equals(name, "AW_PACKETS_BIN", StringComparison.Ordinal) ? wrapper : null);
    }

    public PacketRef Packet(string name)
    {
        var target = Path.Combine(_home.Path, name);
        CopyDirectory(Path.Combine(RepoRoot(), "tests", "fixtures", "line", "gold", "design", "expiry"), target);
        return new PacketRef(PacketKind.Design, target, Repo: _history ??= FixtureRepo());
    }

    // The packet's baseSha is a commit of the scoping fixture's history; the script builds it deterministically.
    private string FixtureRepo()
    {
        var parent = Path.Combine(_home.Path, "history");
        var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(RepoRoot(), "tests", "fixtures", "scoping", "history", "build.sh"));
        start.ArgumentList.Add(parent);
        using var process = Process.Start(start)!;   // why: the program is bash, which starts
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output.Result + error.Result);
        return Path.Combine(parent, "fixture-notes");
    }

    private static void CopyDirectory(string from, string to)
    {
        _ = Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
    }

    private static string BuiltDll()
    {
        var bin = Path.Combine(RepoRoot(), "artifacts", "bin", "Aw.Packets.App");
        var dlls = Directory.Exists(bin) ? Directory.GetFiles(bin, "aw-packets.dll", SearchOption.AllDirectories) : [];
        Assert.True(dlls.Length > 0, "aw-packets is not built: dotnet build src/Packets/Aw.Packets.App");
        return dlls.OrderByDescending(File.GetLastWriteTimeUtc).First();
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
