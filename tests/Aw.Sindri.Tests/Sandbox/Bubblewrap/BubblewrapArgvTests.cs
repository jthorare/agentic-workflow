using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The plan to bubblewrap arguments (sandbox spec §3.1; spike item 1): the order of the stack, the sized tmpfs home, the masks and the environment.</summary>
public sealed class BubblewrapArgvTests
{
    private sealed class FixtureHost : IHostPaths
    {
        public int? Uid() => 1000;

        public string? ReadText(string path) => null;

        public HostPathKind KindOf(string path) => path switch
        {
            "/srv/aw" => HostPathKind.Directory,
            "/var/run/docker.sock" => HostPathKind.File,
            "/etc/ssh" or "/root" => HostPathKind.Directory,
            "/home/owner/.claude/.credentials.json" => HostPathKind.File,
            _ => HostPathKind.Missing,
        };

        // /var/run is a link to /run, as on the box.
        public string Resolve(string path) => path.StartsWith("/var/run/", StringComparison.Ordinal) ? "/run/" + path["/var/run/".Length..] : path;
    }

    private static readonly FixtureHost _host = new();

    private static List<string> Args(SandboxRequest request) => [.. BubblewrapArgv.Build(SandboxPlannerTests.Build(request), _host, secretsFd: false)];

    private static int Find(List<string> args, params string[] sequence)
    {
        for (var i = 0; i + sequence.Length <= args.Count; i++)
        {
            if (args.Skip(i).Take(sequence.Length).SequenceEqual(sequence, StringComparer.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_write_scope_plan_matches_its_golden() =>
        Golden.Match(string.Join('\n', Args(Req.WriteScope)) + "\n", typeof(BubblewrapArgvTests).Assembly, "Sandbox/Bubblewrap/Golden/argv-writescope.txt", Normalization.None);

    [Fact, Trait("Lane", "unit")]
    public void Home_is_a_sized_tmpfs_with_private_permissions() =>
        Assert.True(Find(Args(Req.WriteScope), "--perms", "0700", "--size", "268435456", "--tmpfs", Req.Home) >= 0);

    [Fact, Trait("Lane", "unit")]
    public void The_stack_keeps_the_plan_order_root_first_then_home_then_the_worktree()
    {
        var args = Args(Req.WriteScope);

        var root = Find(args, "--ro-bind", "/", "/");
        var home = Find(args, "--tmpfs", Req.Home);
        var worktree = Find(args, "--bind", Req.Worktree, Req.Worktree);
        Assert.True(root == 3 && root < home && home < worktree);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_original_login_file_is_masked_inside_the_read_only_agent_home()
    {
        var args = Args(Req.WriteScope);

        var home = Find(args, "--ro-bind", Req.Home + "/.claude", Req.Home + "/.claude");
        var mask = Find(args, "--ro-bind", "/dev/null", Req.Home + "/.claude/.credentials.json");
        Assert.True(home >= 0 && mask > home);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_denied_directory_is_an_empty_read_only_tmpfs_and_a_denied_path_under_a_hidden_link_target_adds_nothing()
    {
        var args = Args(Req.WriteScope);

        Assert.True(Find(args, "--perms", "0555", "--tmpfs", "/etc/ssh", "--remount-ro", "/etc/ssh") >= 0);
        Assert.DoesNotContain("/var/run/docker.sock", args, StringComparer.Ordinal);   // /var/run is /run, which is a tmpfs: the socket is not there
    }

    [Fact, Trait("Lane", "unit")]
    public void A_denied_path_the_host_does_not_have_or_that_a_tmpfs_already_hides_adds_nothing()
    {
        var args = Args(Req.WriteScope);

        Assert.DoesNotContain("/home/other", args, StringComparer.Ordinal);
        Assert.DoesNotContain(Req.StateDir, args.Where((_, i) => i > 0 && (string.Equals(args[i - 1], "--tmpfs", StringComparison.Ordinal) || string.Equals(args[i - 1], "--remount-ro", StringComparison.Ordinal))), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Optional_entries_use_the_try_forms_and_the_worktree_and_run_directory_do_not()
    {
        var args = Args(Req.WriteScope);

        Assert.True(Find(args, "--ro-bind-try", Req.Home + "/.cargo", Req.Home + "/.cargo") >= 0);
        Assert.True(Find(args, "--bind-try", Req.Home + "/.nuget/packages", Req.Home + "/.nuget/packages") >= 0);
        Assert.True(Find(args, "--bind", Req.RunDirectory, Req.RunDirectory) >= 0);
        Assert.True(Find(args, "--bind", Req.Worktree, Req.Worktree) >= 0);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_per_launch_directory_is_bound_from_its_spool_path() =>
        Assert.True(Find(Args(Req.WriteScope), "--bind", Req.RunArea + "/spool/logs", Req.StateDir + "/logs") >= 0);

    [Fact, Trait("Lane", "unit")]
    public void The_environment_is_cleared_then_set_from_the_plan_and_the_variables_the_launcher_supplies_are_not_in_it()
    {
        var args = Args(Req.WriteScope);

        Assert.Equal("--clearenv", args[2]);
        Assert.True(Find(args, "--setenv", "CLAUDE_CONFIG_DIR", Req.Home + "/.claude-run") >= 0);
        Assert.DoesNotContain("AW_SINDRI_LAUNCH_SECRET", args, StringComparer.Ordinal);
        Assert.DoesNotContain("GH_TOKEN", args, StringComparer.Ordinal);
        Assert.DoesNotContain("ghp_planted", args, StringComparer.Ordinal);
        Assert.DoesNotContain("--args", args, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_a_secrets_descriptor_the_args_option_comes_after_clearenv_and_before_the_working_directory()
    {
        var args = BubblewrapArgv.Build(SandboxPlannerTests.Build(Req.WriteScope), _host, secretsFd: true).ToList();

        var clear = args.IndexOf("--clearenv");
        var fd = Find(args, "--args", "3");
        Assert.True(clear >= 0 && clear < fd && fd < args.IndexOf("--chdir"));
        Assert.Equal(["AW_SINDRI_LAUNCH_SECRET"], BubblewrapArgv.LauncherSupplied(SandboxPlannerTests.Build(Req.WriteScope)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_session_has_its_own_network_namespace_dies_with_its_parent_and_no_pid_namespace()
    {
        var args = Args(Req.WriteScope);

        Assert.Contains("--unshare-net", args, StringComparer.Ordinal);
        Assert.Contains("--die-with-parent", args, StringComparer.Ordinal);
        Assert.DoesNotContain("--unshare-pid", args, StringComparer.Ordinal);
        Assert.DoesNotContain("--unshare-all", args, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_working_directory_is_the_worktree_or_else_the_run_directory()
    {
        Assert.Equal(["--chdir", Req.Worktree], Args(Req.WriteScope)[^2..]);
        Assert.Equal(["--chdir", Req.RunDirectory], Args(Req.ReadOnly with { Paths = Req.ReadOnly.Paths with { Worktree = null } })[^2..]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_denied_directory_that_later_entries_bind_into_stays_writable_until_they_are_made()
    {
        // The state directory outside a tmpfs home: hidden by an empty tmpfs, which the hook inventory is then bound into.
        var host = Req.Host with { StateDir = "/srv/aw", SindriHome = "/srv/aw/sindri-app" };
        var args = Args(Req.WriteScope with { Host = host, Paths = Req.WriteScope.Paths with { RunDirectory = "/srv/aw/sindri-app/runs/x/s/1", RunArea = "/srv/aw/sindri-app/areas/run-1" } });

        Assert.True(Find(args, "--tmpfs", "/srv/aw") >= 0);
        Assert.False(Find(args, "--remount-ro", "/srv/aw") >= 0);
    }
}
