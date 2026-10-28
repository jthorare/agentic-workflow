using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>Issue #1428: the built-in cursor entry takes an unattended Step's policy and model, so a Cursor-target agent Step launches instead of parking with SND-LAUNCH-013.</summary>
public sealed class CursorPolicyTests
{
    private const string Uuid = "0b1f6c3e-52d4-4c7a-9a58-3d1e7f9a2b10";
    private const string Work = "/work/repo/.claude/worktrees/sindri-1";

    private static readonly StepRef _ref = new("draft", "agent");

    private static AgentEntry Builtin() =>
        LauncherConfigFile.Read(SessionFixtures.Builtins(), user: null).Value.Agents.Single(static e => string.Equals(e.Id.Value, "cursor", StringComparison.Ordinal));

    private static IReadOnlyList<string> Argv(StepPolicy policy, string? model = null) =>
        ArgvBuilder.Build(Builtin(), LaunchMode.Open, new ArgvInputs(
            A.FirstItemId, Work, "sindri step", ArgvBuilder.Seed(A.FirstItemId), SessionId.From(Uuid),
            Step: new StepArgs(model, policy, _ref), PolicyFilePath: Work + "/.cursor/cli.json")).Value;

    private static string[] Rules(StepPolicy policy, string list)
    {
        using var document = JsonDocument.Parse(CursorPolicyFile.Render(policy));
        return document.RootElement.GetProperty("permissions").TryGetProperty(list, out var rules) ? [.. rules.EnumerateArray().Select(static r => r.GetString()!)] : [];
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_only_Step_on_the_cursor_builtin_launches_headless_trusted_sandboxed_and_in_plan_mode()
    {
        var argv = Argv(new StepPolicy(["Read"], StepPermissionMode.ReadOnly, Work), model: "auto");

        Assert.Contains("-p", argv, StringComparer.Ordinal);
        Assert.Contains("--trust", argv, StringComparer.Ordinal);
        Assert.Equal(["--sandbox", "enabled"], argv.SkipWhile(static a => a != "--sandbox").Take(2), StringComparer.Ordinal);
        Assert.Equal(["--workspace", Work], argv.SkipWhile(static a => a != "--workspace").Take(2), StringComparer.Ordinal);
        Assert.Equal(["--mode", "plan"], argv.SkipWhile(static a => a != "--mode").Take(2), StringComparer.Ordinal);
        Assert.Equal(["--model", "auto"], argv.SkipWhile(static a => a != "--model").Take(2), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Step_never_gets_force_or_yolo_and_a_default_mode_adds_no_mode_flag()
    {
        var argv = Argv(new StepPolicy([], StepPermissionMode.Default, Work));

        Assert.DoesNotContain("--force", argv, StringComparer.Ordinal);
        Assert.DoesNotContain("--yolo", argv, StringComparer.Ordinal);
        Assert.DoesNotContain("--mode", argv, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_rules_allow_the_writable_path_and_the_allowed_tools()
    {
        var allow = Rules(new StepPolicy(["Read", "Bash(git status)"], StepPermissionMode.ReadOnly, Work), "allow");

        Assert.Contains($"Write({Work}/**)", allow, StringComparer.Ordinal);
        Assert.Contains("Shell(git status)", allow, StringComparer.Ordinal);
        Assert.Contains("Read(**)", allow, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_plan_Step_gets_no_write_rule()
    {
        var allow = Rules(new StepPolicy(["Read"], StepPermissionMode.Plan, Work), "allow");

        Assert.DoesNotContain(allow, static r => r.StartsWith("Write(", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Denied_and_read_only_paths_become_deny_rules()
    {
        var deny = Rules(new StepPolicy([], StepPermissionMode.Default, Work, DeniedPaths: ["/work/other"], ReadOnlyPaths: ["/work/docs"]), "deny");

        Assert.Contains("Read(/work/other/**)", deny, StringComparer.Ordinal);
        Assert.Contains("Write(/work/other/**)", deny, StringComparer.Ordinal);
        Assert.Contains("Write(/work/docs/**)", deny, StringComparer.Ordinal);
        Assert.DoesNotContain("Read(/work/docs/**)", deny, StringComparer.Ordinal);
    }
}
