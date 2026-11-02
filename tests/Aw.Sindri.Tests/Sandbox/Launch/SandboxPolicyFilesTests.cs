using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The ReadOnly policy files of a sandboxed Step (sandbox spec §5): the plan's inner policy merged into the Claude settings file and the codex overrides.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxPolicyFilesTests
{
    private static readonly InnerPolicy _refuse = new(
        InnerMode.Refuse,
        [
            new InnerRule(InnerRuleEffect.Allow, "Read", string.Empty),
            new InnerRule(InnerRuleEffect.Allow, "Edit", "/run/area/**"),
            new InnerRule(InnerRuleEffect.Allow, "mcp__sindri-step__*", string.Empty),
            new InnerRule(InnerRuleEffect.Deny, "Read", "/home/dev/login/**"),
            new InnerRule(InnerRuleEffect.Deny, "Bash", "git push:*"),
            new InnerRule(InnerRuleEffect.Ask, "Bash", string.Empty),
        ]);

    private static readonly StepPolicy _readOnly = new(["Read", "Grep"], StepPermissionMode.ReadOnly, WritablePath: null);

    private static JsonElement Render(StepPolicy policy, InnerPolicy inner, bool innerSandbox = false) =>
        JsonDocument.Parse(StepPolicyFile.Render(policy, new SandboxPolicyOverlay(inner, InnerSandboxOn: innerSandbox))).RootElement.Clone();

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(static e => e.GetString()!)];

    [Fact]
    public void A_refusing_plan_sets_dontAsk_explicitly()
    {
        var settings = Render(_readOnly, _refuse);

        Assert.Equal("dontAsk", settings.GetProperty("permissions").GetProperty("defaultMode").GetString());
    }

    [Fact]
    public void Bypass_permissions_is_disabled_under_permissions_only()
    {
        var settings = Render(_readOnly, _refuse);

        Assert.False(settings.TryGetProperty("disableBypassPermissionsMode", out _));
        Assert.Equal("disable", settings.GetProperty("permissions").GetProperty("disableBypassPermissionsMode").GetString());
    }

    [Fact]
    public void The_plans_rules_join_the_steps_own_with_path_rules_in_claudes_absolute_form()
    {
        var permissions = Render(_readOnly, _refuse).GetProperty("permissions");

        var allow = Strings(permissions.GetProperty("allow"));
        var deny = Strings(permissions.GetProperty("deny"));
        Assert.Equal(["Read", "Grep", "Edit(//run/area/**)", "mcp__sindri-step__*"], allow);   // the Step's tools first, the duplicate Read once
        Assert.Equal(["Read(//home/dev/login/**)", "Bash(git push:*)"], deny);
    }

    [Fact]
    public void An_ask_rule_is_not_rendered_for_an_unattended_step()
    {
        var text = System.Text.Encoding.UTF8.GetString(StepPolicyFile.Render(_readOnly, new SandboxPolicyOverlay(_refuse, InnerSandboxOn: false)));

        Assert.DoesNotContain("\"ask\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_turn_hooks_are_kept()
    {
        var settings = Render(_readOnly, _refuse);

        foreach (var name in new[] { "Stop", "StopFailure", "UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure" })
        {
            Assert.True(settings.GetProperty("hooks").TryGetProperty(name, out _), name);
        }
    }

    [Fact]
    public void A_plan_mode_step_stays_in_plan_mode()
    {
        var settings = Render(new StepPolicy(["Read"], StepPermissionMode.Plan, WritablePath: null), _refuse);

        Assert.Equal("plan", settings.GetProperty("permissions").GetProperty("defaultMode").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_clis_own_sandbox_follows_the_plan(bool on)
    {
        var settings = Render(_readOnly, _refuse, innerSandbox: on);

        Assert.Equal(on, settings.GetProperty("sandbox").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Without_a_sandbox_the_file_is_what_it_was()
    {
        var plain = System.Text.Encoding.UTF8.GetString(StepPolicyFile.Render(_readOnly));

        Assert.DoesNotContain("disableBypassPermissionsMode", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sandbox\"", plain, StringComparison.Ordinal);
        Assert.Contains("\"defaultMode\": \"plan\"", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void Codex_never_asks_and_a_read_only_step_stays_read_only() =>
        Assert.Equal(["-c", "approval_policy=\"never\""], SandboxPolicyFiles.CodexOverrides(_readOnly));

    [Fact]
    public void Codex_with_a_writable_path_also_loses_its_network()
    {
        var args = SandboxPolicyFiles.CodexOverrides(new StepPolicy([], StepPermissionMode.ReadOnly, "/work/wt"));

        Assert.Equal(["-c", "approval_policy=\"never\"", "-c", "sandbox_workspace_write.network_access=false"], args);
    }
}
