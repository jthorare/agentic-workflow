using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>A service row says why it failed or is paused when the API serves it, and says what it does for every service (UX-pass B-15).</summary>
[Lane(TestLane.Unit)]
public sealed class ServiceRowReasonTests
{
    private static ServiceRowViewModel Shown(string name, ServiceRowView view)
    {
        var row = new ServiceRowViewModel(name);
        row.Show(view);
        return row;
    }

    private static ServiceRowView Failed(string? code, string? detail = null) =>
        new("broken", "5s", Paused: false, LastRun: new ServiceRunView(A.Noon, "failed", code, A.Noon, detail));

    [Fact]
    public void A_failed_row_names_the_code_of_its_run()
    {
        var row = Shown("broken", Failed("SND-SVC-404"));

        Assert.Equal((Labels.ServiceLastRunFailed, "SND-SVC-404", true), (row.Status, row.Reason, row.HasReason));
    }

    [Fact]
    public void A_failed_row_adds_the_served_detail_after_the_code()
    {
        var row = Shown("broken", Failed("SND-SVC-404", "no such command"));

        Assert.Equal("SND-SVC-404: no such command", row.Reason);
    }

    [Fact]
    public void A_failed_run_with_no_code_has_no_reason_to_show()
    {
        var row = Shown("broken", Failed(code: null));

        Assert.Equal((string.Empty, false), (row.Reason, row.HasReason));
    }

    [Fact]
    public void A_paused_row_says_who_paused_it_and_when_when_the_API_serves_both()
    {
        var row = Shown("heartbeat", new ServiceRowView("heartbeat", "5s", Paused: true, PausedBy: "human:ui", PausedAt: A.Noon));

        Assert.Equal((Labels.ServicePaused, "Paused by human:ui at " + StatusText.Time(A.Noon)), (row.Status, row.Reason));
    }

    [Fact]
    public void A_paused_row_with_neither_field_served_has_no_reason()
    {
        var row = Shown("heartbeat", new ServiceRowView("heartbeat", "5s", Paused: true));

        Assert.Equal(string.Empty, row.Reason);
    }

    [Fact]
    public void A_service_the_app_does_not_know_shows_the_served_description()
    {
        var row = Shown("code-index-embed", new ServiceRowView("code-index-embed", "1h", Paused: false, Description: "Embeds the changed files."));

        Assert.Equal("Embeds the changed files.", row.What);
    }

    [Fact]
    public void A_service_with_no_sentence_and_no_description_says_no_description_yet()
    {
        var row = Shown("code-index-embed", new ServiceRowView("code-index-embed", "1h", Paused: false));

        Assert.Equal(Labels.ServiceNoDescription, row.What);
    }

    [Fact]
    public void A_built_in_service_keeps_its_own_sentence_over_a_served_one()
    {
        var row = Shown("heartbeat", new ServiceRowView("heartbeat", "5s", Paused: false, Description: "something else"));

        Assert.Equal(Labels.ServiceWhat("heartbeat"), row.What);
    }
}
