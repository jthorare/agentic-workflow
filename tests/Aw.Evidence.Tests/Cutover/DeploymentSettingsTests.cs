using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Host.Commands;
using Aw.Evidence.Infrastructure.Settings;
using Aw.Evidence.Tests.Parity;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>Cutover unit 1.4: where the env files live, the slot convention read from its variables, the parity record's side, and the version stamp.</summary>
public sealed class DeploymentSettingsTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] values) =>
        name => values.FirstOrDefault(v => v.Name == name).Value;

    [Fact, Trait("Lane", "unit")]
    public void The_env_files_default_to_the_working_directorys_agentic_workflow_folder()
    {
        Assert.Equal(
            ["/app/.agentic-workflow/ui-evidence/.ui-evidence.local.env", "/app/.agentic-workflow/ui-evidence/deployment.env"],
            EnvFileLoader.Files(Env(), "/app"));
    }

    [Fact, Trait("Lane", "unit")]
    public void UI_EVIDENCE_ENV_DIR_moves_both_files_and_UI_EVIDENCE_CONFIG_names_the_local_one()
    {
        Assert.Equal(
            ["/etc/qa/.ui-evidence.local.env", "/etc/qa/deployment.env"],
            EnvFileLoader.Files(Env(("UI_EVIDENCE_ENV_DIR", "/etc/qa")), "/app"));
        Assert.Equal(
            ["/home/me/mine.env", "/etc/qa/deployment.env"],
            EnvFileLoader.Files(Env(("UI_EVIDENCE_ENV_DIR", "/etc/qa"), ("UI_EVIDENCE_CONFIG", "/home/me/mine.env")), "/app"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_slot_convention_needs_its_file_key_count_and_step_and_the_database_is_optional()
    {
        var all = new EnvironmentEvidenceSettings(Env(
            ("UI_EVIDENCE_SLOT_FILE", ".stack-slot.env"), ("UI_EVIDENCE_SLOT_KEY", "STACK_SLOT"), ("UI_EVIDENCE_SLOT_COUNT", "3"),
            ("UI_EVIDENCE_SLOT_PORT_STEP", "10"), ("UI_EVIDENCE_SLOT_DATABASE", "app_{slot}")));
        var noDatabase = new EnvironmentEvidenceSettings(Env(
            ("UI_EVIDENCE_SLOT_FILE", ".stack-slot.env"), ("UI_EVIDENCE_SLOT_KEY", "STACK_SLOT"), ("UI_EVIDENCE_SLOT_COUNT", "3"), ("UI_EVIDENCE_SLOT_PORT_STEP", "10")));
        var noStep = new EnvironmentEvidenceSettings(Env(
            ("UI_EVIDENCE_SLOT_FILE", ".stack-slot.env"), ("UI_EVIDENCE_SLOT_KEY", "STACK_SLOT"), ("UI_EVIDENCE_SLOT_COUNT", "3")));

        Assert.Equal(new SlotSettings(".stack-slot.env", "STACK_SLOT", 3, 10, "app_{slot}"), all.Slots);
        Assert.Null(noDatabase.Slots!.Database);
        Assert.Null(noStep.Slots);
        Assert.Null(new EnvironmentEvidenceSettings(Env()).Slots);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("bad key", "3", "10")]
    [InlineData("STACK_SLOT", "0", "10")]
    [InlineData("STACK_SLOT", "3", "x")]
    public void A_slot_convention_with_a_bad_key_count_or_step_is_not_configured(string key, string count, string step) =>
        Assert.Null(new EnvironmentEvidenceSettings(Env(
            ("UI_EVIDENCE_SLOT_FILE", ".stack-slot.env"), ("UI_EVIDENCE_SLOT_KEY", key), ("UI_EVIDENCE_SLOT_COUNT", count), ("UI_EVIDENCE_SLOT_PORT_STEP", step))).Slots);

    [Theory, Trait("Lane", "unit")]
    [InlineData("base", DataRunAt.Base)]
    [InlineData("head", DataRunAt.Head)]
    public async Task Parity_records_the_side_it_was_at_in_parity_json(string side, DataRunAt expected)
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest());

        var result = await harness.Run(ParityHarness.Request() with { At = expected });

        Assert.Equal(expected, result.Value.Summary.At);
        Assert.Contains($"\"at\":\"{side}\"", System.Text.Encoding.UTF8.GetString(harness.Files.Files["/runs/parity-1/parity.json"]), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_without_a_side_writes_no_at()
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest());

        _ = await harness.Run(ParityHarness.Request());

        Assert.DoesNotContain("\"at\":", System.Text.Encoding.UTF8.GetString(harness.Files.Files["/runs/parity-1/parity.json"]), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("1.0.0+0123456789abcdef0123456789abcdef01234567", "2026-10-23T10:00:00Z", "ui-evidence commit 0123456789abcdef0123456789abcdef01234567, built 2026-10-23T10:00:00Z")]
    [InlineData("1.0.0", null, "ui-evidence commit unknown, built unknown")]
    [InlineData("1.0.0+", "", "ui-evidence commit unknown, built unknown")]
    public void The_version_stamp_names_the_commit_and_the_build_time_or_unknown(string version, string? builtAt, string expected) =>
        Assert.Equal(expected, VersionCommand.Stamp(version, builtAt));
}
