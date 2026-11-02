using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>The 0600 settings file the launcher renders from the target-neutral StepPolicy for Claude Code.</summary>
public sealed class StepPolicyFileTests : IDisposable
{
    private static readonly LaunchId _launch = LaunchId.For(Guid.Empty);

    private readonly string _directory = Path.Combine(Directory.CreateTempSubdirectory("aw-policy").FullName, "policy");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_directory)!, recursive: true);

    public static TheoryData<string, StepPermissionMode> Modes => new()
    {
        { "read-only", StepPermissionMode.ReadOnly },
        { "plan", StepPermissionMode.Plan },
        { "default", StepPermissionMode.Default },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Modes))]
    public void Settings_equal_their_golden(string name, StepPermissionMode mode)
    {
        var text = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read", "Grep"], mode, "/work/repo/.claude/worktrees/sindri-1")));

        Golden.Match(text, typeof(StepPolicyFileTests).Assembly, $"Launch/Golden/policy/claude-settings-{name}.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_stop_hook_touches_the_turn_file_and_a_new_prompt_deletes_it()
    {
        var text = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read"], StepPermissionMode.Default, "/work/wt")));

        Assert.Contains("\"Stop\"", text, StringComparison.Ordinal);
        Assert.Contains("touch \\\"$AW_SINDRI_HOME/launches/$AW_SINDRI_LAUNCH.turn-ended\\\"", text, StringComparison.Ordinal);
        Assert.Contains("\"UserPromptSubmit\"", text, StringComparison.Ordinal);
        Assert.Contains("\"PreToolUse\"", text, StringComparison.Ordinal);   // an agent woken without a prompt is working again
        Assert.Contains("rm -f \\\"$AW_SINDRI_HOME/launches/$AW_SINDRI_LAUNCH.turn-ended\\\"", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_API_error_stop_touches_the_turn_failed_file_and_a_new_prompt_deletes_it()
    {
        var text = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read"], StepPermissionMode.Default, "/work/wt")));

        Assert.Contains("\"StopFailure\"", text, StringComparison.Ordinal);
        Assert.Contains("touch \\\"$AW_SINDRI_HOME/launches/$AW_SINDRI_LAUNCH.turn-failed\\\"", text, StringComparison.Ordinal);
        Assert.Contains("rm -f \\\"$AW_SINDRI_HOME/launches/$AW_SINDRI_LAUNCH.turn-failed\\\"", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tool_call_in_flight_is_marked_until_it_returns_or_fails()
    {
        var text = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read"], StepPermissionMode.Default, "/work/wt")));

        Assert.Contains("touch \\\"$AW_SINDRI_HOME/launches/$AW_SINDRI_LAUNCH.tool-running\\\"", text, StringComparison.Ordinal);
        Assert.Contains("\"PostToolUse\"", text, StringComparison.Ordinal);
        Assert.Contains("\"PostToolUseFailure\"", text, StringComparison.Ordinal);
        Assert.Contains("rm -f \\\"$AW_SINDRI_HOME/launches/$AW_SINDRI_LAUNCH.tool-running\\\"", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_writable_path_no_write_rule_is_added()
    {
        var text = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read"], StepPermissionMode.ReadOnly, WritablePath: null)));

        Assert.DoesNotContain("Edit(", text, StringComparison.Ordinal);
        Assert.DoesNotContain("additionalDirectories", text, StringComparison.Ordinal);
        Assert.Contains("\"defaultMode\": \"plan\"", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Denied_paths_become_read_and_edit_deny_rules()
    {
        var text = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read"], StepPermissionMode.Default, "/work/wt", ["/work/other", "/work/holdout"])));

        Assert.Contains("\"deny\"", text, StringComparison.Ordinal);
        foreach (var path in new[] { "other", "holdout" })
        {
            Assert.Contains($"\"Read(//work/{path}/**)\"", text, StringComparison.Ordinal);
            Assert.Contains($"\"Edit(//work/{path}/**)\"", text, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_denied_paths_there_is_no_deny_key()
    {
        var text = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read"], StepPermissionMode.Default, "/work/wt")));

        Assert.DoesNotContain("deny", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_file_is_written_0600_in_a_0700_directory_and_deleted_with_the_launch()
    {
        var path = StepPolicyFile.Write(_directory, _launch, new StepPolicy([], StepPermissionMode.Plan, WritablePath: null));

        Assert.Equal(StepPolicyFile.PathFor(_directory, _launch), path);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(_directory));
        }

        StepPolicyFile.Delete(_directory, _launch);
        StepPolicyFile.Delete(_directory, _launch);

        Assert.False(File.Exists(path));
    }
}
