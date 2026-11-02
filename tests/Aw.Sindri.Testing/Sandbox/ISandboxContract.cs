using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Core.Sandbox.Plan;
using Xunit;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>
/// The contract every <see cref="ISandbox"/> adapter honours (sandbox spec §10, SB12), run against the pass-through documented behaviour and the real bubblewrap adapter.
/// A case that needs confinement is skipped for an adapter that does not confine (<see cref="Confines"/>). The cases that need the forwarders (the Sindri MCP call, a refused
/// and an allowed host) join when SB-4a and SB-5a wire the egress service; a direct connection to the host's other loopback port is here already.
/// </summary>
public abstract class ISandboxContract : IDisposable
{
    // xUnit v3 reports a test that throws an exception starting with this token as skipped (what Assert.Skip does).
    private const string SkipToken = "$XunitDynamicSkip$";

    private SandboxBox? _box;

    /// <summary>Gets a value indicating whether the adapter confines the session (the pass-through does not).</summary>
    protected abstract bool Confines { get; }

    /// <summary>Gets why the adapter cannot run on this box, or null when it can (the case is then skipped, not failed).</summary>
    protected abstract string? Unavailable { get; }

    /// <summary>The fixture directory: outside /tmp, which a sandbox replaces.</summary>
    protected static string FixtureParent { get; } = Path.Combine(AppContext.BaseDirectory, "sandbox-fixtures");

    /// <summary>Gets the box of this test, made on first use.</summary>
    protected SandboxBox Box => _box ??= SandboxBox.Create(FixtureParent);

    /// <summary>Makes the adapter under test.</summary>
    /// <returns>The adapter.</returns>
    protected abstract ISandbox CreateSandbox();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Removes the box.</summary>
    /// <param name="disposing">True when called from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _box?.Dispose();
        }
    }

    /// <summary>The plan for a write-scope Step builds, names the adapter's mechanism, and wrapping keeps the agent's argv last.</summary>
    [Fact]
    public void Plan_and_wrap_keep_the_agent_argv_last()
    {
        AssumeAvailable();
        var sandbox = CreateSandbox();

        var plan = sandbox.Plan(Box.WriteScope());
        var wrapped = sandbox.Wrap(plan.Value, ["/bin/echo", "agent"]);

        Expect.True(plan.IsSuccess && wrapped.IsSuccess);
        Expect.Sequence(["/bin/echo", "agent"], wrapped.Value.TakeLast(2));
    }

    /// <summary>A write outside the plan's writable set fails: the system and the git config are read-only.</summary>
    [Fact]
    public void Write_outside_the_set_fails()
    {
        AssumeConfines();

        var result = RunInside("( : > /usr/sindri-canary ) 2>/dev/null && exit 1; ( echo x >> '" + Box.Repo + "/.git/config' ) 2>/dev/null && exit 2; exit 0");

        Expect.Equal(0, result.Exit);
    }

    /// <summary>The state directory shows only the hook inventory, and a file the owner keeps there is absent.</summary>
    [Fact]
    public void Denied_path_is_absent()
    {
        AssumeConfines();
        File.WriteAllText(Box.State + "/ledger.db", "secret");
        Directory.CreateDirectory(Box.State + "/meta");

        var result = RunInside("[ ! -e '" + Box.State + "/ledger.db' ] && [ ! -e '" + Box.State + "/meta' ] && [ -d '" + Box.State + "/judge' ] && [ ! -e \"$HOME/.ssh\" ]; echo $?");

        Expect.Equal("0", result.Output.Trim().Split('\n')[^1]);
    }

    /// <summary>A commit and a ref update in the run's directory work; a branch delete elsewhere, a tag, a stash and a push do not.</summary>
    [Fact]
    public void Git_commit_and_update_ref_on_the_run_ref_work_and_the_rest_of_the_repo_is_read_only()
    {
        AssumeConfines();

        var result = RunInside(string.Join('\n',
            "cd '" + Box.Worktree + "' || exit 10",
            "echo a > a.txt && git add a.txt && git commit -q -m a || exit 11",
            "git update-ref refs/heads/sindri/x/1/try HEAD || exit 12",
            "git update-ref refs/heads/sindri/x/1/try HEAD~0 || exit 13",
            "git branch -D other >/dev/null 2>&1 && exit 20",
            "git tag t >/dev/null 2>&1 && exit 21",
            "echo b >> a.txt; git stash >/dev/null 2>&1 && exit 22",
            "git push origin HEAD >/dev/null 2>&1 && exit 23",
            "git update-ref refs/heads/elsewhere HEAD >/dev/null 2>&1 && exit 24",
            "exit 0"));

        Expect.Equal(0, result.Exit);
        Expect.Equal(0, SandboxBox.Run("git", ["-C", Box.Repo, "rev-parse", "--verify", "-q", "refs/heads/other"]).Exit);
    }

    /// <summary>The self-check proves the plan inside the real sandbox, including the three-part hook canary and the judge round trip.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Self_check_passes_with_the_hook_canary_and_the_judge_round_trip()
    {
        AssumeAvailable();
        SkipUnless(!Confines || Box.HasHooks, "this box has no installed hooks or judge wrapper");
        var sandbox = CreateSandbox();
        var plan = sandbox.Plan(Box.WriteScope()).Value;

        var result = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);

        Expect.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Expect.True(SandboxBox.Run("git", ["-C", Box.Repo, "rev-parse", "--verify", "-q", "refs/heads/sindri/x/1/selfcheck"]).Exit != 0);   // the launcher-side cleanup removed it
    }

    /// <summary>The hook canary denies a destructive recursive delete of the root.</summary>
    [Fact]
    public void Hook_canary_denies_a_destructive_recursive_delete_of_the_root()
    {
        AssumeConfines();
        SkipUnless(Box.HasHooks, "this box has no installed hooks");

        var result = RunInside("printf '%s' '{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"rm -rf /\"}}' | \"$HOME/.claude/hooks/block-destructive.sh\" >/dev/null 2>&1; exit $?");

        Expect.Equal(2, result.Exit);
    }

    /// <summary>The environment has no secret-shaped name, whatever the host had.</summary>
    [Fact]
    public void Environment_has_no_secret_names()
    {
        AssumeConfines();

        var result = RunInside("env");

        var names = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => l.Contains('=', StringComparison.Ordinal)).Select(l => l[..l.IndexOf('=', StringComparison.Ordinal)]).ToList();
        Expect.True(EnvironmentAllowlist.Violations(names).Count == 0);
        Expect.True(!names.Contains("GH_TOKEN") && !names.Contains("AW_HEAVY_LOCK_HELD"));
    }

    /// <summary>A restore of a small project works inside the sandbox with the cache paths: the package cache read-write, the CLI home in the scratch, and no network.</summary>
    [Fact]
    public void Dotnet_restore_of_a_fixture_works_with_the_cache_paths()
    {
        AssumeConfines();
        SkipUnless(Box.HasDotnet, "no .NET SDK on this box");
        var cached = CachedPackage();
        SkipUnless(cached is not null, "no package in the shared cache to restore offline");
        var feed = Box.Root + "/empty-feed";
        Directory.CreateDirectory(feed);
        File.WriteAllText(Box.Worktree + "/Fixture.csproj", $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><PackageReference Include=\"{cached!.Value.Id}\" Version=\"{cached.Value.Version}\" /></ItemGroup></Project>");

        var result = RunInside("cd '" + Box.Worktree + "' && dotnet restore Fixture.csproj -p:RestoreSources='" + feed + "' -nologo 2>&1; echo \"exit=$?\"");

        Expect.True(result.Output.TrimEnd().EndsWith("exit=0", StringComparison.Ordinal), result.Output);
        Expect.True(File.Exists(Box.Worktree + "/obj/project.assets.json"));
    }

    /// <summary>A connection to a loopback port the host listens on fails: the session has a loopback of its own, with nothing on it.</summary>
    [Fact]
    public void The_hosts_other_loopback_port_is_unreachable()
    {
        AssumeConfines();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var result = RunInside($"timeout 5 bash -c '(exec 3<>/dev/tcp/127.0.0.1/{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}) 2>/dev/null' && exit 1; exit 0");

        Expect.Equal(0, result.Exit);
    }

    private static (string Id, string Version)? CachedPackage()
    {
        var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured ? configured : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.nuget/packages";
        var id = "microsoft.extensions.dependencyinjection.abstractions";
        var directory = packages + "/" + id;
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var version = Directory.EnumerateDirectories(directory).Select(Path.GetFileName).OfType<string>().Where(v => Directory.Exists($"{directory}/{v}/lib/net10.0")).Order(StringComparer.Ordinal).LastOrDefault();
        return version is null ? null : ("Microsoft.Extensions.DependencyInjection.Abstractions", version);
    }

    // xUnit's dynamic skip: the runner reports a test that throws this token as skipped. (Assert.Skip is in xunit.v3.assert, which this project does not take.)
    private static void SkipUnless(bool condition, string reason)
    {
        if (!condition)
        {
            throw new InvalidOperationException(SkipToken + reason);
        }
    }

    private void AssumeAvailable()
    {
        SkipUnless(SandboxBox.GitAvailable, "git is not installed: the box is a real repository");
        SkipUnless(Unavailable is null, Unavailable ?? string.Empty);
    }

    private void AssumeConfines()
    {
        AssumeAvailable();
        SkipUnless(Confines, "this adapter does not confine the session");
    }

    private CommandResult RunInside(string script)
    {
        var sandbox = CreateSandbox();
        var plan = sandbox.Plan(Box.WriteScope());
        Expect.True(plan.IsSuccess, plan.IsError ? plan.Error.Message : string.Empty);
        var wrapped = sandbox.Wrap(plan.Value, ["/bin/sh", "-c", script]);
        Expect.True(wrapped.IsSuccess, wrapped.IsError ? wrapped.Error.Message : string.Empty);
        return SandboxBox.Run(wrapped.Value[0], [.. wrapped.Value.Skip(1)]);
    }
}
