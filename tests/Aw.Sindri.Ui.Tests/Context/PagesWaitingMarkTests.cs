using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1816: a map page the owner relabeled (node key and page id differ) wears "Waiting for approval" from the profile alone, with nothing saved this app session.</summary>
[Lane(TestLane.Unit)]
public sealed class PagesWaitingMarkTests
{
    private const string RouteKey = "surface:route/GET:/schedule/week";

    [Fact]
    public void A_relabeled_map_page_wears_Waiting_for_approval_after_a_restart()
    {
        var writes = new FakePageWrites { Profile = new ProfileWaiting("h", ["index.pages.schedule-week"]) };
        var section = new PagesSectionViewModel { Writes = writes };
        var answer = PagesSectionTests.Scheduling();
        var route = answer.Nodes![4] with { Key = RouteKey, Owner = true, Confirmed = true, PageId = "schedule-week" };
        answer = answer with { Nodes = [.. answer.Nodes!, route], Pages = [RouteKey, "surface:screen/availability"] };

        section.Show(answer);

        var week = section.Rows.Single(r => r.Key == RouteKey);
        var other = section.Rows.Single(r => r.Key == "surface:screen/availability");
        Expect.True(week.Marks.Contains("Waiting for approval", StringComparer.Ordinal), "the relabeled page waits");
        Expect.True(!other.Marks.Contains("Waiting for approval", StringComparer.Ordinal), "only the waiting page is marked");
    }
}
