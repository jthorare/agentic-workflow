using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The SBPL profile rendered from a Seatbelt plan (sandbox spec §3.1, §3.2, SB-9a): rule order, the two loopback ports, canonical paths, escaping.</summary>
public sealed class SeatbeltProfileTests
{
    private static SandboxPlan Plan(SandboxRequest request)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.Seatbelt, request);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string Render(SandboxPlan plan, Func<string, string>? canonical = null, string runArea = Req.RunArea) => SeatbeltProfile.Render(plan, runArea, canonical);

    private static SandboxPlan WithMounts(params PlanMount[] mounts) => Plan(Req.WriteScope) with { Mounts = mounts };

    private static string[] Lines(string profile) => profile.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static void Match(string text, string golden) => Golden.Match(text, typeof(SeatbeltProfileTests).Assembly, "Sandbox/Plan/Golden/" + golden, Normalization.None);

    [Fact, Trait("Lane", "unit")]
    public void The_profile_allows_by_default_denies_the_network_and_all_writes_first()
    {
        var lines = Lines(Render(Plan(Req.WriteScope)));

        Assert.Equal("(version 1)", lines[0], StringComparer.Ordinal);
        Assert.Equal("(allow default)", lines[1], StringComparer.Ordinal);
        Assert.True(Array.IndexOf(lines, "(deny network*)") < Array.IndexOf(lines, "(deny file-write* (subpath \"/\"))"));
        Assert.Contains("(allow file-write* (literal \"/dev/null\"))", lines, StringComparer.Ordinal);
        Assert.Contains("(allow file-write* (regex #\"^/dev/ttys[0-9]+$\"))", lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_sindris_port_and_the_proxys_port_on_localhost_are_open()
    {
        var lines = Lines(Render(Plan(Req.WriteScope)));

        Assert.Equal(["(allow network-outbound (remote ip \"localhost:7480\"))", "(allow network-outbound (remote ip \"localhost:41234\"))"], lines.Where(l => l.Contains("remote ip", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_deny_then_a_later_allow_under_it_render_in_that_order()
    {
        var lines = Lines(Render(WithMounts(
            new PlanMount("/", PlanMountKind.ReadOnly),
            new PlanMount("/home/o", PlanMountKind.Deny, Reason: "the home"),
            new PlanMount("/home/o/.dotnet", PlanMountKind.ReadOnly, Reason: "the SDK"),
            new PlanMount("/home/o/.nuget", PlanMountKind.ReadWrite, Reason: "packages"))));

        var deny = Array.IndexOf(lines, "(deny file-read* file-write* (subpath \"/home/o\"))");
        var readOnly = Array.IndexOf(lines, "(allow file-read* (subpath \"/home/o/.dotnet\"))");
        var readWrite = Array.IndexOf(lines, "(allow file-read* file-write* (subpath \"/home/o/.nuget\"))");
        Assert.True(deny > 0);
        Assert.True(readOnly > deny);
        Assert.True(readWrite > readOnly);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_only_entry_under_a_writable_one_denies_writes_again()
    {
        var text = Render(WithMounts(
            new PlanMount("/", PlanMountKind.ReadOnly),
            new PlanMount("/work/x", PlanMountKind.ReadWrite),
            new PlanMount("/work/x/.git", PlanMountKind.ReadOnly)));

        Assert.True(text.IndexOf("(allow file-write* (subpath \"/work/x\"))", StringComparison.Ordinal) < text.IndexOf("(deny file-write* (subpath \"/work/x/.git\"))", StringComparison.Ordinal));
        Assert.DoesNotContain("(allow file-read* (subpath \"/work/x/.git\"))", text, StringComparison.Ordinal);   // nothing denied it, so no read rule
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_rule_is_preceded_by_its_reason()
    {
        var lines = Lines(Render(WithMounts(new PlanMount("/work/x", PlanMountKind.ReadWrite, Reason: "the run's worktree"))));
        var rule = Array.IndexOf(lines, "(allow file-write* (subpath \"/work/x\"))");

        Assert.Equal("; the run's worktree", lines[rule - 1]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Links_and_the_root_get_no_rule()
    {
        var text = Render(WithMounts(new PlanMount("/", PlanMountKind.ReadOnly), new PlanMount("/run/h/.venv", PlanMountKind.Link, "/home/o/.venv")));

        Assert.DoesNotContain(".venv", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(subpath \"/\")\n(allow", text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/var/folders/ab/T/x", "/private/var/folders/ab/T/x")]
    [InlineData("/tmp", "/private/tmp")]
    [InlineData("/etc/hosts", "/private/etc/hosts")]
    [InlineData("/variable/x", "/variable/x")]
    [InlineData("/tmpfs", "/tmpfs")]
    [InlineData("/Users/o/etc", "/Users/o/etc")]
    [InlineData("/private/var/x", "/private/var/x")]
    public void The_default_canonicaliser_rewrites_exactly_var_tmp_and_etc(string path, string expected) => Assert.Equal(expected, SeatbeltProfile.MacOsCanonical(path));

    [Fact, Trait("Lane", "unit")]
    public void Rules_use_the_canonical_path_and_a_given_canonicaliser_replaces_the_default()
    {
        var plan = WithMounts(new PlanMount("/var/folders/T/run", PlanMountKind.ReadWrite));

        Assert.Contains("(allow file-write* (subpath \"/private/var/folders/T/run\"))", Render(plan), StringComparison.Ordinal);
        Assert.Contains("(allow file-write* (subpath \"/real/run\"))", Render(plan, _ => "/real/run"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_quote_or_backslash_in_a_path_is_escaped_inside_the_string()
    {
        var text = Render(WithMounts(new PlanMount("/work/a\"b\\c", PlanMountKind.ReadWrite)));

        Assert.Contains("(subpath \"/work/a\\\"b\\\\c\")", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_newline_in_a_reason_cannot_start_a_rule()
    {
        var text = Render(WithMounts(new PlanMount("/work/x", PlanMountKind.ReadWrite, Reason: "one\n(allow file-write* (subpath \"/\"))")));

        Assert.DoesNotContain("\n(allow file-write* (subpath \"/\"))", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_write_scope_step_matches_its_golden() => Match(Render(Plan(Req.WriteScope)), "seatbelt-writescope.sb");

    [Fact, Trait("Lane", "unit")]
    public void An_attended_session_with_home_entries_matches_its_golden() =>
        Match(Render(Plan(Req.Attended with { Settings = new SandboxSettings(Fs: new SandboxFs(HomeAllow: [new HomeAllowEntry(".venv", MountMode.ReadWrite), new HomeAllowEntry(".config/gh", MountMode.ReadOnly)]), Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = new(CachePaths: [".cache/pip"]) }) })), "seatbelt-attended.sb");

    [Fact, Trait("Lane", "unit")]
    public void Unix_sockets_are_allowed_under_the_run_area_and_the_short_temp_directory_only_and_the_paths_are_canonical()
    {
        var plan = Plan(Req.WriteScope);

        var lines = Lines(Render(plan, p => p.Replace("/home/owner", "/c", StringComparison.Ordinal)));

        foreach (var area in new[] { "(subpath \"/c/.agentic-workflow/sindri-app/workflow/areas/run-1\")", "(subpath \"" + SandboxPaths.SeatbeltTemp(plan.Launch) + "\")" })
        {
            Assert.Contains($"(allow network-bind (local unix-socket {area}))", lines, StringComparer.Ordinal);
            Assert.Contains($"(allow network-inbound (local unix-socket {area}))", lines, StringComparer.Ordinal);
            Assert.Contains($"(allow network-outbound (remote unix-socket {area}))", lines, StringComparer.Ordinal);
        }

        Assert.Equal(6, lines.Count(l => l.Contains("unix-socket", StringComparison.Ordinal) && !l.StartsWith(';')));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_run_area_comes_from_the_caller_not_from_where_the_plan_file_sits()
    {
        var plan = Plan(Req.WriteScope);

        var text = Render(plan, runArea: "/elsewhere/area");

        Assert.Contains("(allow network-bind (local unix-socket (subpath \"/elsewhere/area\")))", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"(local unix-socket (subpath \"{Req.RunArea}\"))", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_short_temp_directory_is_writable()
    {
        var plan = Plan(Req.WriteScope);

        Assert.Contains($"(allow file-write* (subpath \"{SandboxPaths.SeatbeltTemp(plan.Launch)}\"))", Render(plan), StringComparison.Ordinal);
    }

    private static SandboxRequest MacShaped()
    {
        const string Home = "/Users/owner";
        const string Repo = Home + "/repos/web";
        const string Worktree = Repo + "/.claude/worktrees/sindri-x-1";
        const string Area = Home + "/.agentic-workflow/sindri-app/workflow/areas/run-1";
        static string Mac(string v) => v.Replace("/home/owner", Home, StringComparison.Ordinal);
        var host = Req.Host with
        {
            Home = Home,
            StateDir = Home + "/.agentic-workflow",
            SindriHome = Home + "/.agentic-workflow/sindri-app",
            CsBinDir = Mac(Req.CsBinDir),
            IsMacOs = true,
            Environment = Req.Host.Environment.ToDictionary(e => e.Key, e => Mac(e.Value), StringComparer.Ordinal),
            OtherHomes = ["/Users/other"],
        };
        var login = new PreparedLogin(
            ModelLoginSource.OwnerCopy,
            [new LoginMount(Home + "/.claude", Home + "/.claude", MountMode.ReadOnly), new LoginMount(Area + "/login", Home + "/.claude-run", MountMode.ReadWrite)],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["CLAUDE_CONFIG_DIR"] = Home + "/.claude-run" },
            [Home + "/.claude-run"],
            new LoginHandle(Area + "/login"));
        return Req.WriteScope with
        {
            Paths = new SandboxPaths(Repo, Repo + "/.git", Worktree, host.SindriHome + "/workflow/runs/x/s/1", Area),
            Policy = new StepPolicy([], StepPermissionMode.Default, Worktree),
            Host = host,
            Login = login,
        };
    }

    [Fact, Trait("Lane", "unit")]
    public void A_mac_shaped_write_scope_step_matches_its_golden()
    {
        var text = Render(Plan(MacShaped()), runArea: "/Users/owner/.agentic-workflow/sindri-app/workflow/areas/run-1");

        Match(text, "seatbelt-mac.sb");
        Assert.Contains("(allow file-read* file-write* (subpath \"/Users/owner/repos/web/.claude/worktrees/sindri-x-1\"))", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("(deny file-read* file-write* (subpath \"/Users/owner\"))", StringComparison.Ordinal) < text.IndexOf("(subpath \"/Users/owner/repos/web/.claude/worktrees/sindri-x-1\")", StringComparison.Ordinal));
    }
}
