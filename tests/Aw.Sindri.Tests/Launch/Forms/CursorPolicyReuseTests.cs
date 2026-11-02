using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;


namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>Issue #1428 review: a worktree is reused, so a launch whose host died must not leave a .cursor/cli.json that refuses every later cursor Step there; and the rule mapping fails loudly.</summary>
public sealed class CursorPolicyReuseTests : IDisposable
{
    private static readonly LaunchId _a = LaunchId.From("LCH-0000000000000000000000000000000a");
    private static readonly LaunchId _b = LaunchId.From("LCH-0000000000000000000000000000000b");
    private static readonly StepPolicy _plan = new(["Read"], StepPermissionMode.Plan, WritablePath: null);

    private readonly string _root = Directory.CreateTempSubdirectory("aw-cursor-reuse").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Policy => Path.Combine(_root, "policy");

    private string Worktree => Path.Combine(_root, "wt");

    private string Config => CursorPolicyFile.PathIn(Worktree);

    private static string[] Allow(StepPolicy policy)
    {
        using var document = JsonDocument.Parse(CursorPolicyFile.Render(policy));
        return [.. document.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray().Select(static r => r.GetString()!)];
    }

    [Fact, Trait("Lane", "unit")]
    public void A_launch_after_a_dead_launch_in_the_same_worktree_replaces_its_file()
    {
        _ = CursorPolicyFile.Write(Policy, _a, Worktree, _plan, static _ => false);

        var second = CursorPolicyFile.Write(Policy, _b, Worktree, new StepPolicy(["Grep"], StepPermissionMode.Plan, WritablePath: null), static _ => false);   // A's host died: A is not live

        Assert.Equal((Config, false, true), (second.Value, File.Exists(CursorPolicyFile.SidecarFor(Policy, _a)), File.Exists(CursorPolicyFile.SidecarFor(Policy, _b))));
        Assert.Equal(CursorPolicyFile.Render(new StepPolicy(["Grep"], StepPermissionMode.Plan, WritablePath: null)), File.ReadAllBytes(Config));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_launch_after_a_live_launch_in_the_same_worktree_is_refused_as_another_launchs()
    {
        _ = CursorPolicyFile.Write(Policy, _a, Worktree, _plan, static _ => false);
        var before = File.ReadAllBytes(Config);

        var second = CursorPolicyFile.Write(Policy, _b, Worktree, _plan, static launch => launch.Equals(_a));

        Assert.True(second.IsError);
        Assert.Contains("another launch", second.Error.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(Config));   // a byte[] inside a tuple compares by reference, so the two are checked apart
        Assert.True(File.Exists(CursorPolicyFile.SidecarFor(Policy, _a)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cli_json_no_sidecar_names_is_the_repos_own_even_when_other_launches_have_sidecars()
    {
        _ = CursorPolicyFile.Write(Policy, _a, Path.Combine(_root, "other-wt"), _plan, static _ => false);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
        File.WriteAllText(Config, "{}");

        var written = CursorPolicyFile.Write(Policy, _b, Worktree, _plan, static _ => false);

        Assert.True(written.IsError);
        Assert.Contains("never overwrites", written.Error.Message, StringComparison.Ordinal);
        Assert.Equal("{}", File.ReadAllText(Config));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_stale_sweep_deletes_a_dead_launchs_config_with_its_sidecar_and_keeps_a_live_ones()
    {
        var now = KnownInstants.Epoch.AddHours(1);
        var options = new LauncherOptions(_root);
        _ = CursorPolicyFile.Write(options.PolicyDirectory, _a, Path.Combine(_root, "wt-a"), _plan, static _ => false);
        _ = CursorPolicyFile.Write(options.PolicyDirectory, _b, Path.Combine(_root, "wt-b"), _plan, static _ => false);
        foreach (var launch in new[] { _a, _b })
        {
            File.SetLastWriteTimeUtc(CursorPolicyFile.SidecarFor(options.PolicyDirectory, launch), now.AddMinutes(-5).UtcDateTime);
        }

        var removed = new LaunchFileStore(options).SweepStale(now, TimeSpan.FromSeconds(60), static launch => launch.Equals(_b));

        Assert.Equal((1, false, true, true), (removed, File.Exists(CursorPolicyFile.PathIn(Path.Combine(_root, "wt-a"))), File.Exists(CursorPolicyFile.PathIn(Path.Combine(_root, "wt-b"))), File.Exists(CursorPolicyFile.SidecarFor(options.PolicyDirectory, _b))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_Bash_maps_to_Shell_and_a_bare_Edit_or_Write_to_Write()
        => Assert.Equal(["Shell(*)", "Write(**)", "Write(**)"], Allow(new StepPolicy(["Bash", "Edit", "Write"], StepPermissionMode.Default, WritablePath: null)));

    [Fact, Trait("Lane", "unit")]
    public void A_bare_MultiEdit_or_NotebookEdit_maps_to_Write_and_a_bare_WebFetch_to_WebFetch_and_the_argument_forms_keep_their_argument()
        => Assert.Equal(
            ["Write(**)", "Write(**)", "WebFetch(*)", "Write(src/**)", "WebFetch(domain:example.com)"],
            Allow(new StepPolicy(["MultiEdit", "NotebookEdit", "WebFetch", "MultiEdit(src/**)", "WebFetch(domain:example.com)"], StepPermissionMode.Default, WritablePath: null)));

    [Fact, Trait("Lane", "unit")]
    public void A_tool_with_no_Cursor_rule_type_is_refused_not_passed_through()
    {
        var policy = new StepPolicy(["Read", "Task"], StepPermissionMode.Plan, WritablePath: null);

        var written = CursorPolicyFile.Write(Policy, _a, Worktree, policy, static _ => false);

        _ = Assert.Throws<ArgumentException>(() => CursorPolicyFile.Render(policy));
        Assert.True(written.IsError);
        Assert.Contains("Task", written.Error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Config));
    }
}
