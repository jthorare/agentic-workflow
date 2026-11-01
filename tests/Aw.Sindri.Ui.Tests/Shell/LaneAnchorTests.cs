using System.Globalization;
using System.Xml.Linq;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Theme;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The lane anchors (docs/csharp/sindri-ui.md "Lane anchors"): where each marker sits, so two lanes' merges touch different lines. The checks assert order, never adjacency to a neighbour, and each runs again over a sample of the insertion its marker prescribes.</summary>
[Lane(TestLane.Unit)]
public sealed class LaneAnchorTests
{
    private static string[] Lines(string path) => File.ReadAllLines(Path.Combine(UiSource.Root, path));

    private static int Find(string[] lines, string text) => Array.FindIndex(lines, l => l.Contains(text, StringComparison.Ordinal));

    private static string[] Insert(string[] lines, int at, params string[] add) => [.. lines[..at], .. add, .. lines[at..]];

    private const string ClientPath = "src/Sindri/Aw.Sindri.Ui/Api/SindriApiClient.cs";
    private const string SheetPath = "src/Sindri/Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.Sheet.cs";

    private static void AssertClient(string[] lines)
    {
        var u3 = Find(lines, "LANE ANCHOR U3'");
        var u8 = Find(lines, "LANE ANCHOR U8");
        var recipe = Find(lines, "Task<Result<RecipeView>> Recipe(");
        var events = Find(lines, " Events(");

        Assert.True(recipe >= 0 && recipe < u3, "the U3' marker belongs after Recipe(...), found at index " + u3.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(u3 + 1, u8);
        Assert.True(u8 < events, "the markers sit before Events(...)");
    }

    [Fact]
    public void The_client_anchors_follow_the_public_reads_and_point_at_no_other_file()
    {
        var lines = Lines(ClientPath);

        AssertClient(lines);
        Assert.DoesNotContain(lines, l => l.Contains("Calls.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void The_client_anchors_survive_a_read_above_the_first_marker_and_a_send_below_the_second()
    {
        var lines = Lines(ClientPath);
        var u3 = Find(lines, "LANE ANCHOR U3'");
        var withRead = Insert(lines, u3, "    /// <summary>Reads X.</summary>", "    public Task<Result<RecipeView>> Nodes(Uri api, CancellationToken cancellationToken) => Get<RecipeView>(api, \"x\", cancellationToken);", string.Empty);
        var u8 = Find(withRead, "LANE ANCHOR U8");
        var both = Insert(withRead, u8 + 1, "    /// <summary>Sends Y.</summary>", "    public Task<Result<RecipeView>> Send(Uri api, CancellationToken cancellationToken) => Get<RecipeView>(api, \"y\", cancellationToken);");

        AssertClient(both);
    }

    private static bool IsArmOrBlank(string line)
    {
        var text = line.Trim();
        // An arm may wrap: `DeepLinkKind.A` on one line and `or DeepLinkKind.B => false,` on the next.
        var kind = text.StartsWith("DeepLinkKind.", StringComparison.Ordinal) || text.StartsWith("or DeepLinkKind.", StringComparison.Ordinal);
        return text.Length == 0 || (kind && (text.EndsWith("=> false,", StringComparison.Ordinal) || !text.Contains("=>", StringComparison.Ordinal)));
    }

    private static void AssertSwitch(string[] lines)
    {
        var property = Find(lines, "IsSheetPlaceholderShown =>");
        var u3 = Find(lines, "LANE ANCHOR U3'");
        var u8 = Find(lines, "LANE ANCHOR U8");
        var u10 = Find(lines, "LANE ANCHOR U10");
        var u9 = Find(lines, "LANE ANCHOR U9");
        var u11a = Find(lines, "LANE ANCHOR U11a");
        var fallback = Find(lines, "_ => true");

        Assert.True(property >= 0 && property < u3 && u8 == u3 + 1 && u8 < u10 && u10 < u9 && u9 < u11a && u11a < fallback, "U3' and U8 are adjacent inside the switch, U10 follows U8's arms, U9 follows U10's and U11a U9's, all above its '_ => true' arm");
        Assert.All(lines[(u8 + 1)..u10], l => Assert.True(IsArmOrBlank(l), "between u8 and u10 only `… => false,` arms: " + l));
        Assert.All(lines[(u10 + 1)..u9], l => Assert.True(IsArmOrBlank(l), "between u10 and u9 only `… => false,` arms: " + l));
        Assert.All(lines[(u9 + 1)..u11a], l => Assert.True(IsArmOrBlank(l), "between u9 and u11a only `… => false,` arms: " + l));
        Assert.All(lines[(u11a + 1)..fallback], l => Assert.True(IsArmOrBlank(l), "between u11a and the fallback only `… => false,` arms: " + l));
    }

    [Fact]
    public void Both_lane_markers_sit_inside_the_placeholder_switch_just_before_its_default_arm()
        => AssertSwitch(File.ReadAllLines(Path.Combine(UiSource.Root, SheetPath)));

    [Fact]
    public void The_switch_anchors_survive_an_arm_above_the_first_marker_and_one_below_the_second()
    {
        var lines = File.ReadAllLines(Path.Combine(UiSource.Root, SheetPath));
        var withAbove = Insert(lines, Find(lines, "LANE ANCHOR U3'"), "        DeepLinkKind.NeedsYou or DeepLinkKind.Decision => false,");
        var both = Insert(withAbove, Find(withAbove, "LANE ANCHOR U8") + 1, "        DeepLinkKind.Workflow => false,");
        var withTen = Insert(both, Find(both, "LANE ANCHOR U10") + 1, "        DeepLinkKind.Recipe => false,");
        var all = Insert(withTen, Find(withTen, "LANE ANCHOR U9") + 1, "        DeepLinkKind.Node => false,");
        all = Insert(all, Find(all, "LANE ANCHOR U11a") + 1, "        DeepLinkKind.Session => false,");

        AssertSwitch(all);
    }

    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Named(XDocument doc, string name) => doc.Descendants().Single(e => string.Equals((string?)e.Attribute(_x + "Name"), name, StringComparison.Ordinal));

    private static XComment Marker(XDocument doc, string text) => doc.DescendantNodes().OfType<XComment>().Single(c => c.Value.Contains(text, StringComparison.Ordinal));

    private static void AssertWindow(XDocument doc)
    {
        var sheet = Named(doc, "Sheet");
        var content = Named(doc, "SheetContent");
        var u3 = Marker(doc, "LANE ANCHOR U3'");
        var u8 = Marker(doc, "LANE ANCHOR U8");
        var u10 = Marker(doc, "LANE ANCHOR U10");
        var u11a = Marker(doc, "LANE ANCHOR U11a");
        var u9 = Marker(doc, "LANE ANCHOR U9");

        Assert.Contains(content, sheet.Descendants());
        Assert.Same(content, u3.Parent);
        Assert.Same(content, u8.Parent);
        Assert.Same(u8, u3.NextNode);
        Assert.Same(content, u10.Parent);
        Assert.True(u10.IsAfter(u8), "the U10 marker follows U8's content");
        Assert.Same(content, u9.Parent);
        Assert.True(u9.IsAfter(u10), "the U9 marker follows U10's content");
        Assert.Same(content, u11a.Parent);
        Assert.True(u11a.IsAfter(u9), "the U11a marker follows U9's content");
    }

    private static XDocument Window() => XDocument.Load(Path.Combine(UiSource.Library, "Views", "Shell", "ShellWindow.axaml"));

    [Fact]
    public void The_window_has_one_sheet_layer_and_the_lane_anchors_sit_inside_its_content() => AssertWindow(Window());

    [Fact]
    public void The_window_anchors_survive_a_block_above_the_first_marker_and_one_below_the_second()
    {
        var doc = Window();
        Marker(doc, "LANE ANCHOR U3'").AddBeforeSelf(new XElement("Border", new XElement("TextBlock", new XAttribute("Text", "a"))));
        Marker(doc, "LANE ANCHOR U8").AddAfterSelf(new XElement("Border", new XElement("TextBlock", new XAttribute("Text", "b"))));
        Marker(doc, "LANE ANCHOR U10").AddAfterSelf(new XElement("Border", new XElement("TextBlock", new XAttribute("Text", "c"))));

        AssertWindow(doc);
    }

    [Fact]
    public void Every_marker_says_where_to_insert_and_not_between_the_markers()
    {
        var files = new[]
        {
            "src/Sindri/Aw.Sindri.Ui/Api/ISindriApi.cs", "src/Sindri/Aw.Sindri.Ui/Api/SindriApiClient.cs", "src/Sindri/Aw.Sindri.Ui/Api/SindriUiJsonContext.cs",
            "src/Sindri/Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.cs", "src/Sindri/Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.Sheet.cs", "src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml",
            "tests/Aw.Sindri.Ui.Testing/FakeSindriApi.Workflow.cs", "tests/Aw.Sindri.Ui.Testing/FakeSindriHandler.cs", "tests/Aw.Sindri.Ui.Testing/SindriApiContract.cs", "tests/Aw.Sindri.Ui.Testing/SindriApiContract.Workflow.cs",
            "docs/csharp/sindri-ui.md",
        };
        var markers = files.SelectMany(f => File.ReadAllLines(Path.Combine(UiSource.Root, f)).Where(l => l.Contains("LANE ANCHOR U3':", StringComparison.Ordinal) || l.Contains("LANE ANCHOR U8:", StringComparison.Ordinal)).Select(l => (File: f, Line: l))).ToList();

        Assert.True(markers.Count >= 2 * files.Length);
        Assert.All(markers, m => Assert.True(
            m.Line.Contains("never between the markers", StringComparison.Ordinal) && (m.Line.Contains("immediately above", StringComparison.Ordinal) || m.Line.Contains("immediately below", StringComparison.Ordinal)),
            m.File + ": " + m.Line));
    }

    private static HashSet<string> Kinds(string path, string marker)
    {
        var line = Lines(path).Single(l => l.Contains(marker, StringComparison.Ordinal));
        var from = line.IndexOf("(kinds: ", StringComparison.Ordinal);
        Assert.True(from >= 0, path + ": " + marker + " names no kinds");
        var list = line[(from + "(kinds: ".Length)..];
        return [.. list[..list.IndexOf(')', StringComparison.Ordinal)].Split(',', StringSplitOptions.TrimEntries)];
    }

    [Fact]
    public void The_two_markers_claim_real_kinds_and_no_kind_twice_and_the_window_says_the_same()
    {
        var u3 = Kinds(SheetPath, "LANE ANCHOR U3'");
        var u8 = Kinds(SheetPath, "LANE ANCHOR U8");
        var real = Enum.GetNames<DeepLinkKind>().ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(u3);
        Assert.NotEmpty(u8);
        Assert.Subset(real, u3);
        Assert.Subset(real, u8);
        Assert.Empty(u3.Intersect(u8, StringComparer.Ordinal));
        Assert.Equal(u3.Order(StringComparer.Ordinal), Kinds("src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml", "LANE ANCHOR U3'").Order(StringComparer.Ordinal));
        Assert.Equal(u8.Order(StringComparer.Ordinal), Kinds("src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml", "LANE ANCHOR U8").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_U10_marker_follows_U8_and_claims_Recipe()
    {
        var u10 = Kinds(SheetPath, "LANE ANCHOR U10");

        Assert.Equal(["Recipe"], u10);
        Assert.Equal(u10, Kinds("src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml", "LANE ANCHOR U10"));
        Assert.Empty(u10.Intersect(Kinds(SheetPath, "LANE ANCHOR U8"), StringComparer.Ordinal));
        Assert.Empty(u10.Intersect(Kinds(SheetPath, "LANE ANCHOR U3'"), StringComparer.Ordinal));
        string[] files = [SheetPath, "src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml", "docs/csharp/sindri-ui.md"];
        Assert.All(files, f =>
        {
            var line = Lines(f).Single(l => l.Contains("LANE ANCHOR U10", StringComparison.Ordinal));
            Assert.Contains("never between the markers", line, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void The_U11a_marker_follows_and_claims_Session()
    {
        const string Window = "src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml";
        var u11a = Kinds(SheetPath, "LANE ANCHOR U11a");

        Assert.Equal(["Session"], u11a);
        Assert.Equal(u11a, Kinds(Window, "LANE ANCHOR U11a"));
        foreach (var other in (string[])["LANE ANCHOR U3'", "LANE ANCHOR U8", "LANE ANCHOR U10"])
        {
            Assert.Empty(u11a.Intersect(Kinds(SheetPath, other), StringComparer.Ordinal));
        }

        string[] markered = [SheetPath, Window, "docs/csharp/sindri-ui.md", "src/Sindri/Aw.Sindri.Ui/Api/ISindriApi.cs", "src/Sindri/Aw.Sindri.Ui/Api/SindriUiJsonContext.cs", "tests/Aw.Sindri.Ui.Testing/FakeSindriHandler.cs"];
        Assert.All(markered, f =>
        {
            var line = Lines(f).Single(l => l.Contains("LANE ANCHOR U11a", StringComparison.Ordinal));
            Assert.Contains("immediately below this marker, never between the markers", line, StringComparison.Ordinal);
        });
        foreach (var f in (string[])[SheetPath, Window])
        {
            Assert.DoesNotContain("Session,", string.Join('\n', Lines(f).Where(l => l.Contains("LANE ANCHOR U10", StringComparison.Ordinal))), StringComparison.Ordinal);
        }
    }

    // arch-3: every kind has an owner: one of the two lanes, U2's item sheet, a page link (Home's Needs you, Settings' setup checklist, Projects and a project's sheet, the Context page's page sheet), or the phase that renders it later.
    [Fact]
    public void Every_deep_link_kind_is_claimed_by_a_lane_or_named_as_later()
    {
        string[] owned = ["Item", "HomeNeedsYou", "Setup", "ContextItem", "Projects", "Project", "ProjectAdd", "PageSheet", "PinSheet", "AddSeveralPages"];
        string[] later = ["System", "Service", "Job"];
        var claimed = Kinds(SheetPath, "LANE ANCHOR U3'").Concat(Kinds(SheetPath, "LANE ANCHOR U8")).Concat(Kinds(SheetPath, "LANE ANCHOR U10")).Concat(Kinds(SheetPath, "LANE ANCHOR U9")).Concat(Kinds(SheetPath, "LANE ANCHOR U11a")).Concat(owned).Concat(later);

        Assert.Equal(Enum.GetNames<DeepLinkKind>().Order(StringComparer.Ordinal), claimed.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_U9_marker_follows_U10_and_claims_Node()
    {
        var u9 = Kinds(SheetPath, "LANE ANCHOR U9");

        Assert.Equal(["Node"], u9);
        Assert.Equal(u9, Kinds("src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml", "LANE ANCHOR U9"));
        Assert.Empty(u9.Intersect(Kinds(SheetPath, "LANE ANCHOR U10"), StringComparer.Ordinal));
        string[] files = [SheetPath, "src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml", "docs/csharp/sindri-ui.md"];
        Assert.All(files, f =>
        {
            var line = Lines(f).Single(l => l.Contains("LANE ANCHOR U9", StringComparison.Ordinal));
            Assert.Contains("never between the markers", line, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void The_four_markers_claim_no_kind_twice_and_the_window_says_the_same()
    {
        var markers = new[] { "LANE ANCHOR U3'", "LANE ANCHOR U8", "LANE ANCHOR U10", "LANE ANCHOR U9" };
        var all = markers.SelectMany(m => Kinds(SheetPath, m)).ToList();

        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.All(markers, m => Assert.Equal(Kinds(SheetPath, m).Order(StringComparer.Ordinal), Kinds("src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml", m).Order(StringComparer.Ordinal)));
    }

    // The API interface and the JSON context carry a U9 marker below U8's; the context calls and records are inserted at it.
    [Theory]
    [InlineData("src/Sindri/Aw.Sindri.Ui/Api/ISindriApi.cs")]
    [InlineData("src/Sindri/Aw.Sindri.Ui/Api/SindriUiJsonContext.cs")]
    public void The_U9_marker_sits_below_U8s_and_says_where_to_insert(string path)
    {
        var lines = Lines(path);
        var u8 = Find(lines, "LANE ANCHOR U8");
        var u9 = Find(lines, "LANE ANCHOR U9");

        Assert.True(u8 >= 0 && u9 > u8, path + ": the U9 marker belongs below U8's");
        Assert.Contains("never between the markers", lines[u9], StringComparison.Ordinal);
        Assert.Contains("immediately below", lines[u9], StringComparison.Ordinal);
    }
}
