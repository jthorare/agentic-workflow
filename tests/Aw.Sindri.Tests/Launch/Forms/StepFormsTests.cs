using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>The L-17 forms: an unattended Open adds the Step's model and policy arguments after the expanded open template, and nothing else ever does.</summary>
public sealed class StepFormsTests
{
    private const string Uuid = "0b1f6c3e-52d4-4c7a-9a58-3d1e7f9a2b10";
    private const string Settings = "/home/sindri/launch/policy/LCH-1.settings.json";

    private static readonly StepRef _ref = new("build", "agent");

    private static ArgvInputs Inputs(StepArgs? step = null, string? settings = null) =>
        new(A.FirstItemId, "/work/repo", "sindri step", ArgvBuilder.Seed(A.FirstItemId), SessionId.From(Uuid), Step: step, PolicyFilePath: settings);

    private static StepArgs Model(string id) => new(Model: id, Policy: null, _ref);

    private static StepArgs Policy(StepPermissionMode mode, string? writable = "/work/repo/.claude/worktrees/sindri-1") =>
        new(Model: null, new StepPolicy(["Read", "Grep"], mode, writable), _ref);

    [Fact, Trait("Lane", "unit")]
    public void Forms_add_arguments_only_when_unattended()
    {
        var human = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Open, Inputs()).Value;
        var step = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Open, Inputs(Model("opus"))).Value;

        Assert.Equal([.. human, "--model", "opus"], step, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Human_open_resume_and_attach_get_no_form_arguments()
    {
        var step = new StepArgs("opus", new StepPolicy([], StepPermissionMode.Plan, WritablePath: null), _ref);
        var resumed = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Resume, Inputs(step, Settings)).Value;
        var plainResume = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Resume, Inputs()).Value;
        var human = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Open, Inputs()).Value;
        var attach = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Attach, Inputs(step, Settings));

        Assert.Equal(plainResume, resumed, StringComparer.Ordinal);
        Assert.DoesNotContain("--model", human, StringComparer.Ordinal);
        Assert.Equal("SND-REQ-001", attach.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Form_arguments_follow_the_expanded_open_template()
    {
        var argv = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs(new StepArgs("gpt-x", new StepPolicy([], StepPermissionMode.Plan, WritablePath: null), _ref))).Value;

        Assert.Equal(["codex", "-C", "/work/repo", ArgvBuilder.Seed(A.FirstItemId), "-m", "gpt-x", "-c", "sandbox_mode=\"read-only\""], argv, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_model_flag_twice_the_last_one_wins()
    {
        var entry = AgentEntries.ClaudeCode with { Open = ["{bin}", "--session-id", "{sessionId}", "-n", "{name}", "--model", "opus", "{seed}"] };

        var argv = ArgvBuilder.Build(entry, LaunchMode.Open, Inputs(Model("sonnet"))).Value;

        Assert.Equal(["--model", "opus"], argv.Skip(5).Take(2), StringComparer.Ordinal);
        Assert.Equal(["--model", "sonnet"], argv.TakeLast(2), StringComparer.Ordinal);
        Assert.Equal(2, argv.Count(static a => a == "--model"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Claude_policy_adds_the_settings_file_the_launcher_wrote()
    {
        var argv = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Open, Inputs(Policy(StepPermissionMode.ReadOnly), Settings)).Value;

        Assert.Equal(["--settings", Settings], argv.TakeLast(2), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Claude_policy_without_a_settings_path_is_refused()
    {
        var argv = ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Open, Inputs(Policy(StepPermissionMode.Plan)));

        Assert.Equal("SND-REQ-001", argv.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Codex_plan_policy_is_a_read_only_sandbox()
    {
        var argv = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs(Policy(StepPermissionMode.Plan))).Value;

        Assert.Equal(["-c", "sandbox_mode=\"read-only\""], argv.TakeLast(2), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Codex_read_only_allows_only_the_one_writable_path_and_default_changes_nothing()
    {
        var readOnly = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs(Policy(StepPermissionMode.ReadOnly, "/w/\"odd\\dir"))).Value;
        var noWrite = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs(Policy(StepPermissionMode.ReadOnly, writable: null))).Value;
        var plain = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs()).Value;
        var standard = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs(Policy(StepPermissionMode.Default))).Value;

        Assert.Equal(["-c", "sandbox_mode=\"workspace-write\"", "-c", "sandbox_workspace_write.writable_roots=[\"/w/\\\"odd\\\\dir\"]"], readOnly.TakeLast(4), StringComparer.Ordinal);
        Assert.Equal(["-c", "sandbox_mode=\"read-only\""], noWrite.TakeLast(2), StringComparer.Ordinal);
        Assert.Equal(plain, standard, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Requested_model_on_form_none_is_SND_LAUNCH_013()
    {
        var argv = ArgvBuilder.Build(AgentEntries.Aider, LaunchMode.Open, Inputs(Model("opus")));

        Assert.Equal("SND-LAUNCH-013", argv.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Requested_policy_on_form_none_is_SND_LAUNCH_013_and_nothing_starts()
    {
        var argv = ArgvBuilder.Build(AgentEntries.Aider, LaunchMode.Open, Inputs(Policy(StepPermissionMode.Plan)));

        Assert.True(argv.IsError);
        Assert.Equal("SND-LAUNCH-013", argv.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_cursor_forms_are_reserved_even_when_an_entry_names_them()
    {
        var entry = AgentEntries.Cursor with { ModelForm = ModelForm.CursorFlag, PolicyForm = PolicyForm.CursorConfig };

        Assert.Equal("SND-LAUNCH-013", ArgvBuilder.Build(entry, LaunchMode.Open, Inputs(Model("auto"))).Error.Code);
        Assert.Equal("SND-LAUNCH-013", ArgvBuilder.Build(entry, LaunchMode.Open, Inputs(Policy(StepPermissionMode.Plan))).Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("--dangerously-skip-permissions")]
    public void A_model_id_that_is_empty_or_starts_with_a_dash_is_refused(string model) =>
        Assert.Equal("SND-REQ-001", ArgvBuilder.Build(AgentEntries.ClaudeCode, LaunchMode.Open, Inputs(Model(model))).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_relative_writable_path_is_refused() =>
        Assert.Equal("SND-REQ-001", ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs(Policy(StepPermissionMode.ReadOnly, "relative/dir"))).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_step_that_asks_for_neither_adds_nothing()
    {
        var human = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs()).Value;
        var step = ArgvBuilder.Build(AgentEntries.Codex, LaunchMode.Open, Inputs(new StepArgs(Model: null, Policy: null, _ref))).Value;

        Assert.Equal(human, step, StringComparer.Ordinal);
    }
}
