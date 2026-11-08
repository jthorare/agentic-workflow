using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri status' prints the views and health lines, and exits 1 unless the health is healthy (plan unit E, G11).</summary>
public sealed class StatusTextTests
{
    private static readonly ProfileHash _hash = ProfileHash.From("3f1c2a" + new string('0', 58));
    private static readonly ControlEndpoint _endpoint = new(new Uri("http://127.0.0.1:7410"), 42, 3);

    private static GetStatusResponse Status(ViewsStatus views, HealthReport health) =>
        new(Running: true, 42, 3, KnownInstants.Epoch,
            new ProfileStatus(_hash, Problem: null, "/home/profile.yaml"), [])
        { Views = views, Health = health };

    private static ViewsStatus Fresh() => new(Cursor: 9, LedgerHead: 9, KnownInstants.Epoch, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null);

    private static HealthReport Healthy() => new(HealthWord.Healthy, [], Sentence: null);

    private static string Render(GetStatusResponse status) =>
        SummaryText.Render(status, _endpoint, ui: null).ReplaceLineEndings("\n");

    [Fact, Trait("Lane", "unit")]
    public void Fresh_views_and_a_healthy_word_print_one_line_each()
    {
        var text = Render(Status(Fresh(), Healthy()));

        Assert.Contains("  views     cursor 9 of 9\n", text, StringComparison.Ordinal);
        Assert.Contains("  health    healthy\n", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rebuild_and_a_stall_say_so_in_words_with_the_fix()
    {
        var rebuilding = Render(Status(Fresh() with { Cursor = 3, Rebuilding = true }, Healthy()));
        var stalled = Render(Status(Fresh() with { Cursor = 6, Stalled = true, StalledAt = 7, StalledType = "service.ran" }, Healthy()));

        Assert.Contains("  views     rebuilding (cursor 3 of 9)\n", rebuilding, StringComparison.Ordinal);
        Assert.Contains("  views     stalled at 7 (service.ran): run `sindri views rebuild`\n", stalled, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Views_that_cannot_be_updated_name_no_event()
    {
        var failing = Render(Status(Fresh() with { Stalled = true, StalledType = "SND-VIEW-001: views.db can't be opened" }, Healthy()));
        var ledger = Render(Status(Fresh() with { Stalled = true, StalledType = "SND-LEDGER-002: the ledger can't be read" }, Healthy()));

        Assert.Contains("  views     can't be updated (SND-VIEW-001: views.db can't be opened): run `sindri views rebuild`\n", failing, StringComparison.Ordinal);
        Assert.Contains("  views     can't be updated (SND-LEDGER-002: the ledger can't be read): run `sindri doctor`\n", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("stalled at", failing, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_health_problem_prints_on_its_own_line_under_the_word()
    {
        var health = new HealthReport(HealthWord.Degraded,
            [new("SND-SCHED-001", "Service heartbeat failed 1 min ago", "service heartbeat failed"),
             new("SND-VIEW-001", "Views can't be updated: run `sindri views rebuild`", "the views can't be updated")],
            "Sindri is degraded: service heartbeat failed and the views can't be updated.");

        var text = Render(Status(Fresh(), health));

        Assert.Contains("  health    degraded\n              SND-SCHED-001 Service heartbeat failed 1 min ago\n              SND-VIEW-001 Views can't be updated: run `sindri views rebuild`\n", text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HealthWord.Healthy, 0)]
    [InlineData(HealthWord.Degraded, 1)]
    [InlineData(HealthWord.Stalled, 1)]
    [InlineData(HealthWord.Rebuilding, 1)]
    public void Status_exits_1_unless_the_health_word_is_healthy(HealthWord word, int expected)
    {
        var status = Status(Fresh(), new HealthReport(word, [], Sentence: null));

        Assert.Equal(expected, Verbs.StatusExitCode(status));
    }
}
