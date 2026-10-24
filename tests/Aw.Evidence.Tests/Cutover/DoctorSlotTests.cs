using Aw.Evidence.Core.Settings;
using Aw.Evidence.Features.Doctor;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// Cutover gap G-d (spec §3.3), doctor half: the worktree-slot checks scripts/doctor.sh made (SKILL 'Worktree stacks'): the slot file
/// in the worktree names slot N, slot N serves the web app on 3000 + step * N and the API on the API port + step * N, its database is
/// {slot}-templated, both ports must be served from that worktree, and a healthy doctor prints the --host and the database to use.
/// The doctor reads the slot file through IRunFiles and the slot settings from IEvidenceSettings.
/// </summary>
public sealed class DoctorSlotTests
{
    private const string Worktree = "/work/wt";

    private readonly FakeStackProbe _probe = new() { Title = "App" };
    private readonly InMemoryRunFiles _files = new();
    private readonly FakeEvidenceSettings _settings = new()
    {
        AppTitle = "App",
        ApiPort = 5002,
        Slots = new SlotSettings(".stack-slot.env", "STACK_SLOT", Count: 3, PortStep: 10, Database: "app_{slot}"),
    };

    public DoctorSlotTests()
    {
        _ = _probe.Databases.Add("app_2");
        _ = _probe.OpenPorts.Add("localhost:5022");
        _probe.ListenerDirectories[3020] = $"{Worktree}/web";
        _probe.ListenerDirectories[5022] = $"{Worktree}/api";
        SlotFile("OTHER=1\nSTACK_SLOT=2\n");
    }

    private void SlotFile(string content) =>
        _ = _files.With($"{Worktree}/.stack-slot.env", System.Text.Encoding.UTF8.GetBytes(content));

    private async Task<DoctorResponse> Check(string? worktree = Worktree) =>
        (await new DoctorService(_probe, _settings, _files).Handle(new DoctorRequest(worktree), CancellationToken.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_healthy_slot_prints_the_host_and_the_database_to_use()
    {
        var report = await Check();

        Assert.True(report.Healthy, string.Join("; ", report.Failures));
        Assert.Equal(new DoctorSlot(2, Worktree, "http://localhost:3020", "app_2"), report.Slot);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_probes_the_slots_api_port_not_the_main_one()
    {
        _ = _probe.OpenPorts.Remove("localhost:5022");

        var report = await Check();

        Assert.False(report.Healthy);
        Assert.Contains("api-server (:5022) is not accepting connections", report.Failures, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_port_served_from_another_checkout_is_a_foreign_stack_and_refused()
    {
        _probe.ListenerDirectories[3020] = "/work/main";

        var report = await Check();

        Assert.False(report.Healthy);
        Assert.Contains(report.Failures, static f => f.StartsWith(":3020 is served from /work/main, not /work/wt", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_listener_inside_a_subdirectory_of_the_worktree_is_this_worktrees_and_a_sibling_with_the_same_prefix_is_not()
    {
        _probe.ListenerDirectories[3020] = $"{Worktree}/apps/web";
        _probe.ListenerDirectories[5022] = "/work/wt-other/api";

        var report = await Check();

        var failure = Assert.Single(report.Failures);
        Assert.StartsWith(":5022 is served from /work/wt-other/api", failure, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_database_that_does_not_exist_says_to_prepare_it_in_the_worktree()
    {
        _probe.Databases.Clear();

        var report = await Check();

        Assert.False(report.Healthy);
        Assert.Contains(report.Failures, static f => f.StartsWith("database app_2 does not exist", StringComparison.Ordinal) && f.Contains(Worktree, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_slot_database_configured_the_database_check_is_skipped_and_no_database_is_printed()
    {
        _settings.Slots = _settings.Slots! with { Database = null };
        _probe.Databases.Clear();

        var report = await Check();

        Assert.True(report.Healthy, string.Join("; ", report.Failures));
        Assert.Null(report.Slot!.Database);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("STACK_SLOT=0\n")]
    [InlineData("STACK_SLOT=4\n")]            // above UI_EVIDENCE_SLOT_COUNT
    [InlineData("STACK_SLOT=two\n")]
    [InlineData("OTHER=1\n")]                 // no key
    public async Task A_slot_file_without_a_slot_between_1_and_the_count_is_unhealthy_and_says_where(string content)
    {
        SlotFile(content);

        var report = await Check();

        Assert.False(report.Healthy);
        Assert.Contains(report.Failures, static f => f.Contains($"{Worktree}/.stack-slot.env has no STACK_SLOT between 1 and 3", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_named_worktree_without_the_slot_file_says_to_claim_a_slot_there_first()
    {
        var report = await Check("/work/no-slot");

        Assert.False(report.Healthy);
        Assert.Contains(report.Failures, static f => f.Contains("/work/no-slot/.stack-slot.env", StringComparison.Ordinal) && f.Contains("claim a stack slot", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_worktree_with_no_slots_configured_is_refused_not_silently_checked_against_3000()
    {
        _settings.Slots = null;

        var report = await Check();

        Assert.False(report.Healthy);
        Assert.Contains(report.Failures, static f => f.Contains("no worktree slots are configured", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("app-{slot}")]
    [InlineData("app_{slot}'; select 1; --")]
    public async Task A_slot_database_name_that_is_not_letters_digits_and_underscores_is_refused_before_any_query(string template)
    {
        _settings.Slots = _settings.Slots! with { Database = template };

        var report = await Check();

        Assert.False(report.Healthy);
        Assert.Contains(report.Failures, static f => f.Contains("letters, digits and _", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_worktree_doctor_checks_the_main_stack_and_reports_no_slot()
    {
        var report = await Check(worktree: null);

        Assert.True(report.Healthy, string.Join("; ", report.Failures));
        Assert.Null(report.Slot);
    }
}
