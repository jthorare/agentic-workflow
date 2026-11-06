using System.Text;
using System.Text.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>The turn hooks of a skill Step (skill flows S-0): both targets touch the turn markers and the prompt marker; Codex reads them from the worktree's project file and runs with the trust flag.</summary>
public sealed class SkillTurnHooksTests
{
    private static readonly StepRef _ref = new("fix", "step.skill.review");

    private static JsonElement CodexHooks() => JsonDocument.Parse(CodexTurnHooks.Render()).RootElement.GetProperty("hooks");

    private static string Command(JsonElement hooks, string eventName) => hooks.GetProperty(eventName)[0].GetProperty("hooks")[0].GetProperty("command").GetString() ?? string.Empty;

    [Fact, Trait("Lane", "unit")]
    public void The_codex_hooks_file_has_stop_prompt_pre_and_post_tool_hooks_on_the_launch_markers()
    {
        var hooks = CodexHooks();

        Assert.Contains(".turn-ended", Command(hooks, "Stop"), StringComparison.Ordinal);
        Assert.Contains(".prompted", Command(hooks, "UserPromptSubmit"), StringComparison.Ordinal);
        Assert.Contains(".tool-running", Command(hooks, "PreToolUse"), StringComparison.Ordinal);
        Assert.Contains(".tool-running", Command(hooks, "PostToolUse"), StringComparison.Ordinal);
        Assert.Equal(".*", hooks.GetProperty("PreToolUse")[0].GetProperty("matcher").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_codex_skill_step_runs_with_the_hook_trust_flag_and_a_plain_step_does_not()
    {
        static StepArgs Step(bool markers) => new(Model: null, new StepPolicy([], StepPermissionMode.Default, WritablePath: null) { TurnMarkers = markers }, _ref);

        var skill = StepForms.Arguments(AgentEntries.Codex, Step(markers: true), policyFilePath: null);
        var plain = StepForms.Arguments(AgentEntries.Codex, Step(markers: false), policyFilePath: null);

        Assert.Contains(CodexTurnHooks.TrustFlag, skill.Value, StringComparer.Ordinal);
        Assert.DoesNotContain(CodexTurnHooks.TrustFlag, plain.Value, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_codex_hooks_file_is_written_into_the_worktree_and_rewritten_when_it_is_ours()
    {
        using var worktree = new TempDirectory();

        var first = await CodexTurnHooks.Write(worktree.Path, CancellationToken.None);
        var second = await CodexTurnHooks.Write(worktree.Path, CancellationToken.None);

        Assert.False(first.IsError);
        Assert.False(second.IsError);
        Assert.Equal(CodexTurnHooks.Render(), await File.ReadAllBytesAsync(Path.Combine(worktree.Path, ".codex", "hooks.json"), TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_hooks_file_the_repo_already_has_refuses_the_launch()
    {
        using var worktree = new TempDirectory();
        _ = Directory.CreateDirectory(Path.Combine(worktree.Path, ".codex"));
        await File.WriteAllTextAsync(Path.Combine(worktree.Path, ".codex", "hooks.json"), "{}", TestContext.Current.CancellationToken);

        var written = await CodexTurnHooks.Write(worktree.Path, CancellationToken.None);

        Assert.True(written.IsError);
        Assert.Equal(SindriErrors.RequestInvalid.Code, written.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_claude_settings_of_a_skill_step_mark_every_prompt_and_a_plain_step_does_not()
    {
        var skill = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read"], StepPermissionMode.Default, "/work/wt") { TurnMarkers = true }));
        var plain = Encoding.UTF8.GetString(StepPolicyFile.Render(new StepPolicy(["Read"], StepPermissionMode.Default, "/work/wt")));

        Assert.Contains("touch \\\"$AW_SINDRI_HOME/launches/$AW_SINDRI_LAUNCH.prompted\\\"", skill, StringComparison.Ordinal);
        Assert.DoesNotContain(".prompted", plain, StringComparison.Ordinal);
    }
}
