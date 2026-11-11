using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// #2776: the Needs-you inbox builds only the rows in view. Every page sits in the shell's one scroll viewer, so the list is
/// opened inside the real shell and its realized row views are counted.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouInboxVirtualizationTests : FrameHost
{
    private const int Total = 1000;

    // The "large" golden's rows (a few kinds, so a few groups) repeated to Total rows, each with its own id.
    private static RouteAnswer Thousand()
    {
        var view = JsonNode.Parse(GoldenFiles.Api("needs-you", "large"))!;
        var rows = view["rows"]!.AsArray();
        var seed = rows.Select(r => r!.DeepClone()).ToList();
        rows.Clear();
        for (var i = 0; i < Total; i++)
        {
            var row = seed[i % seed.Count].DeepClone();
            row["id"] = string.Create(CultureInfo.InvariantCulture, $"{(string)row["id"]!}#{i}");
            rows.Add(row);
        }

        return new RouteAnswer(System.Net.HttpStatusCode.OK, view.ToJsonString());
    }

    [Fact]
    public Task NeedsYou_inbox_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        Server.NeedsYouView = Thousand();
        var (window, shell) = await Connected(s => s.NeedsYou.Count >= Total, width: 1280, height: 800);

        shell.OpenNeedsYouInbox();
        Settle(window);

        Assert.Equal(Total, shell.Inbox.Groups.Sum(g => g.Rows.Count));
        var built = Named<Control>(window, "NeedsYouGroups").GetVisualDescendants().OfType<NeedsYouInboxRow>().Count();
        Assert.True(built < 100, string.Create(CultureInfo.InvariantCulture, $"the inbox built {built} of {Total} rows"));
    });
}
