using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Xunit;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>
/// A throw-away box for the sandbox contract (sandbox spec §10): a real git repository with a run worktree and a run branch, a state directory with the hook inventory,
/// a run area, a net directory and a fake login copy, all under one directory that is not in /tmp (a bubblewrap sandbox replaces /tmp). The home is the owner's real
/// home by default, so the real toolchains, hooks and the judge wrapper are what the sandbox binds; the state directory is the fixture's, so nothing of the owner's
/// state is touched. Nothing here ever reads or writes the owner's login: the login is a fake in the fixture.
/// </summary>
public sealed partial class SandboxBox : IDisposable
{
    private const string Namespace = "sindri/x/1";

    private readonly string _projectsEntry;

    private SandboxBox(string root, string home, bool realHome)
    {
        Root = root;
        Home = home;
        Repo = root + "/repo";
        Worktree = Repo + "/.claude/worktrees/sindri-x-1";
        State = root + "/state";
        SindriHome = State + "/sindri-app";
        RunArea = SindriHome + "/areas/run-1";
        RunDirectory = SindriHome + "/runs/x/s/1";
        NetDir = ShortRuntimeDirectory() + "/sb2-" + Path.GetFileName(root)[^8..];
        LoginCopy = root + "/login";
        _projectsEntry = realHome ? $"{home}/.claude/projects/{ProjectKeyOf(Worktree)}" : string.Empty;
    }

    /// <summary>Gets the fixture's directory.</summary>
    public string Root { get; }

    /// <summary>Gets the home the sandbox replaces.</summary>
    public string Home { get; }

    /// <summary>Gets the repo.</summary>
    public string Repo { get; }

    /// <summary>Gets the run worktree.</summary>
    public string Worktree { get; }

    /// <summary>Gets the state directory (not under the home's default, so the plan tells the hooks where it is).</summary>
    public string State { get; }

    /// <summary>Gets the Sindri home.</summary>
    public string SindriHome { get; }

    /// <summary>Gets the run area.</summary>
    public string RunArea { get; }

    /// <summary>Gets the run directory.</summary>
    public string RunDirectory { get; }

    /// <summary>Gets the net directory.</summary>
    public string NetDir { get; }

    /// <summary>Gets the fake login copy's directory.</summary>
    public string LoginCopy { get; }

    /// <summary>Gets a value indicating whether the box has the owner's installed hooks and a judge wrapper, so the hook canary can run.</summary>
    public bool HasHooks => File.Exists(Home + "/.claude/hooks/block-destructive.sh") && ToolkitDir().Length > 0;

    /// <summary>Gets a value indicating whether git and the .NET SDK the restore fixture needs are on this box.</summary>
    public bool HasDotnet => File.Exists(Home + "/.dotnet/dotnet") || Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 };

    /// <summary>Gets a value indicating whether git is installed: the box is a real repository.</summary>
    public static bool GitAvailable { get; } = ProbeGit();

    /// <summary>Makes a box under <paramref name="parent"/> with the owner's real home.</summary>
    /// <param name="parent">A directory outside /tmp (the test assembly's output).</param>
    /// <returns>The box.</returns>
    public static SandboxBox Create(string parent) => Create(parent, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Makes a box under <paramref name="parent"/> with the given home.</summary>
    /// <param name="parent">A directory outside /tmp.</param>
    /// <param name="home">The home directory the sandbox replaces.</param>
    /// <returns>The box.</returns>
    public static SandboxBox Create(string parent, string home)
    {
        var real = string.Equals(home, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.Ordinal);
        var box = new SandboxBox(Path.Combine(parent, "sandbox-" + RealTime.NewGuid().ToString("N")[..8]), home, real);
        box.Build();
        return box;
    }

    /// <summary>Makes the request for a Step with a write scope on this box.</summary>
    /// <returns>The request.</returns>
    public SandboxRequest WriteScope() => new(
        LaunchId.From("LCH-" + new string('a', 32)),
        ItemId.From("ITM-" + new string('b', 32)),
        AgentId.From("claude-code"),
        RepoName.From("web"),
        InstanceAddress.From("T-1/item/1"),
        new SandboxPaths(Repo, Repo + "/.git", Worktree, RunDirectory, RunArea),
        Namespace,
        new StepPolicy([], StepPermissionMode.Default, Worktree),
        Attended: false,
        StepTimeout: TimeSpan.FromMinutes(30),
        LongMax: null,
        SlotHeld: false,
        new SandboxNetFacts(NetDir, 7480, 41234),
        Host(),
        Settings: null,
        Login: FakeLogin());

    /// <summary>Makes the request for a Step without a write scope.</summary>
    /// <returns>The request.</returns>
    public SandboxRequest ReadOnly() => WriteScope() with { Policy = new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null) };

    /// <summary>Gets the box's host facts: the real PATH and toolchain variables, the fixture's state directory, and a planted secret the plan must leave out.</summary>
    /// <returns>The facts.</returns>
    public SandboxHostFacts Host()
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["GH_TOKEN"] = "ghp_planted", ["AW_HEAVY_LOCK_HELD"] = "1" };
        foreach (var name in new[] { "PATH", "LANG", "TERM", "USER", "SHELL", "DOTNET_ROOT", "NVM_DIR" })
        {
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
            {
                environment[name] = value;
            }
        }

        environment["PATH"] = environment.GetValueOrDefault("PATH", "/usr/local/bin:/usr/bin:/bin");
        return new SandboxHostFacts(Home, State, SindriHome, ToolkitDir(), Uid(), TotalMemory(), Environment.ProcessorCount, IsMacOs: false, environment, []);
    }

    /// <summary>Gets the login a copy source would hand over, with a fake login in the box: the agent home read-only and the copy directory at the config path.</summary>
    /// <returns>The prepared login.</returns>
    public PreparedLogin FakeLogin() => new(
        ModelLoginSource.OwnerCopy,
        [new LoginMount(Home + "/.claude", Home + "/.claude", MountMode.ReadOnly), new LoginMount(LoginCopy, Home + "/.claude-run", MountMode.ReadWrite)],
        new Dictionary<string, string>(StringComparer.Ordinal) { ["CLAUDE_CONFIG_DIR"] = Home + "/.claude-run" },
        [Home + "/.claude-run"],
        new LoginHandle(LoginCopy));

    /// <summary>Runs a program on the host (git in the fixture, a probe) and waits for it.</summary>
    /// <param name="program">The program.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="workingDirectory">Where it runs, or null.</param>
    /// <returns>Its exit code and what it printed.</returns>
    public static CommandResult Run(string program, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var start = new ProcessStartInfo(program, arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        if (workingDirectory is not null)
        {
            start.WorkingDirectory = workingDirectory;
        }

        using var process = Process.Start(start)!;   // why: a missing program is a failed test run, reported as the exception
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
        }

        process.WaitForExit();
#pragma warning disable RS0030 // the child has exited; its pipes reach end of file
        return new CommandResult(process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
#pragma warning restore RS0030
    }

    /// <summary>Removes the box and the one directory a plan makes in the owner's agent home.</summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }

        if (Directory.Exists(NetDir))
        {
            Directory.Delete(NetDir, recursive: true);
        }

        if (_projectsEntry.Length > 0 && Directory.Exists(_projectsEntry) && !Directory.EnumerateFileSystemEntries(_projectsEntry).Any())
        {
            Directory.Delete(_projectsEntry);
        }
    }

    // A unix socket path is limited to 104 bytes, so the net directory lives under the runtime directory, as in the product, not under the fixture root.
    private static string ShortRuntimeDirectory() =>
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtime ? runtime : "/tmp/sindri-" + Uid().ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("[^A-Za-z0-9]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NonAlphanumeric { get; }

    [GeneratedRegex("exec node \"(?<dir>[^\"]+)/judge/dist/cli\\.js\"", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JudgeWrapper { get; }

    private static bool ProbeGit()
    {
        try
        {
            return Run("git", ["--version"]).Exit == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string ProjectKeyOf(string path) => NonAlphanumeric.Replace(path, "-");

    private string ToolkitDir()
    {
        var wrapper = Home + "/.local/bin/judge";
        if (!File.Exists(wrapper))
        {
            return string.Empty;
        }

        var match = JudgeWrapper.Match(File.ReadAllText(wrapper));
        return match.Success && File.Exists(match.Groups["dir"].Value + "/judge/dist/cli.js") ? match.Groups["dir"].Value : string.Empty;
    }

    private static int Uid()
    {
        var line = File.ReadAllLines("/proc/self/status").First(l => l.StartsWith("Uid:", StringComparison.Ordinal));
        return int.Parse(line.Split('\t', StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);
    }

    private static long TotalMemory()
    {
        var line = File.ReadAllLines("/proc/meminfo").First(l => l.StartsWith("MemTotal:", StringComparison.Ordinal));
        return long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture) * 1024;
    }

    private void Build()
    {
        foreach (var directory in new[] { Repo, State + "/judge", State + "/locks", State + "/context-guard/state", State + "/bin-r1b", RunArea, RunDirectory, NetDir, LoginCopy })
        {
            _ = Directory.CreateDirectory(directory);
        }

        File.WriteAllText(LoginCopy + "/.credentials.json", "{\"claudeAiOauth\":{\"accessToken\":\"fake\"}}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(LoginCopy + "/.credentials.json", UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        string[][] git =
        [
            ["init", "-q", "-b", "main"],
            ["-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "--allow-empty", "-m", "base"],
            ["branch", "other"],
            ["worktree", "add", "-q", Worktree, "-b", Namespace + "/work"],
        ];
        foreach (var args in git)
        {
            var (exit, output) = Run("git", ["-c", "commit.gpgsign=false", .. args], Repo);
            if (exit != 0)
            {
                throw new InvalidOperationException($"git {string.Join(' ', args)} failed in the fixture: {output}");
            }
        }
    }
}
