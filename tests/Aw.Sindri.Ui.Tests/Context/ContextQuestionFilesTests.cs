using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>A question that resolves always shows the files the engine returned, and a stage that draws no nodes says so (#2974, slice 1).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextQuestionFilesTests
{
    private const string Question = "How does aw-meta lane-recycle type into a pane?";
    private const string Recycle = "src/Meta/Aw.Meta.Host/Commands/LaneRecycleCommand.cs";
    private const string Pane = "src/Meta/Aw.Meta.Infrastructure/Processes/TmuxLanePane.cs";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The shape of the engine's answer for the lane-recycle question: one anchor, code files, a record that is not a file, and a map that draws nothing.
    private static RouteAnswer LaneRecycleAnswer() => new(HttpStatusCode.OK, new JsonObject
    {
        ["anchors"] = new JsonArray("thing:lanes"),
        ["suggestedAnchors"] = new JsonArray(),
        ["sections"] = new JsonArray(
            Section("graph", Item("thing", "thing:lanes", "lanes", text: null, why: "anchor")),
            Section("code",
                Item("code", "code:" + Recycle, Recycle, "implements feature:agentic-workflow.src.meta.aw_meta_host", "implements"),
                Item("code", "code:" + Pane, Pane, "implements feature:agentic-workflow.src.meta.aw_meta_infrastructure", "implements")),
            Section("records", Item("record", node: null, "Sindri's Agents page", "One steer route.", "mentions"))),
    }.ToJsonString());

    private static JsonObject Section(string name, params JsonObject[] items) => new() { ["name"] = name, ["items"] = new JsonArray(items) };

    private static JsonObject Item(string kind, string? node, string title, string? text, string why) =>
        new() { ["kind"] = kind, ["node"] = node, ["title"] = title, ["text"] = text, ["why"] = new JsonArray(why) };

    // A map that holds the anchor but draws no node: the golden answer with every band emptied.
    private static RouteAnswer EmptyMap(LensesQuery _)
    {
        var map = GoldenFiles.Node("context-lenses", "map-tabs");
        var bands = map["bands"]!;
        bands["intent"]!["records"] = new JsonArray();
        bands["intent"]!["decisions"] = new JsonArray();
        bands["product"]!["own"] = new JsonArray();
        bands["product"]!["neighbors"] = new JsonArray();
        bands["product"]!["affects"] = new JsonArray();
        bands["product"]!["edges"] = new JsonArray();
        bands["code"]!["files"] = new JsonArray();
        map["edges"] = new JsonArray();
        return new RouteAnswer(HttpStatusCode.OK, map.ToJsonString());
    }

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task A_resolved_question_whose_map_draws_nothing_lists_the_files_and_says_why_the_stage_is_empty()
    {
        var server = new FakeSindriApi { QueryAnswer = _ => LaneRecycleAnswer(), LensesAnswer = EmptyMap };
        var page = Page(server);
        page.AnchorText = Question;

        await page.Submit(Token);

        Assert.Equal([Recycle, Pane], page.AnswerFiles.Select(f => f.Path).ToList());   // the record is not a file; the order is the answer's
        Assert.Equal(["implements feature:agentic-workflow.src.meta.aw_meta_host", "implements feature:agentic-workflow.src.meta.aw_meta_infrastructure"], page.AnswerFiles.Select(f => f.Reason).ToList());
        Assert.Equal(("src/Meta/Aw.Meta.Host/Commands/", "LaneRecycleCommand.cs"), (page.AnswerFiles[0].Directory, page.AnswerFiles[0].Name));
        Assert.True(page.HasAnswerFiles);
        Assert.Empty(page.Map!.Nodes);
        Assert.True(page.HasStageMessage);
    }

    [Fact]
    public async Task With_the_whole_map_on_the_stage_a_question_whose_lens_draws_nothing_lists_its_files_and_never_says_the_map_draws_nothing()
    {
        var server = new FakeSindriApi { QueryAnswer = _ => LaneRecycleAnswer(), LensesAnswer = EmptyMap };
        var page = Page(server);
        await page.Opened(Token);   // the page's normal state: the whole map is read and on the stage
        page.AnchorText = Question;

        await page.Submit(Token);

        Assert.True(page.ShowWhole);
        Assert.Equal([Recycle, Pane], page.AnswerFiles.Select(f => f.Path).ToList());
        Assert.False(page.HasStageMessage, page.StageMessage);   // the stage draws the whole map, so "draws nothing" would be false
    }

    [Fact]
    public async Task A_long_answer_lists_its_first_25_files_and_says_how_many_more_in_words()
    {
        var files = Enumerable.Range(1, 30).Select(n => $"src/Many/File{n:D2}.cs").ToList();
        var answer = new JsonObject
        {
            ["anchors"] = new JsonArray("thing:lanes"),
            ["suggestedAnchors"] = new JsonArray(),
            ["sections"] = new JsonArray(Section("code", [.. files.Select(f => Item("code", "code:" + f, f, text: null, "implements"))])),
        };
        var page = Page(new FakeSindriApi { QueryAnswer = _ => new RouteAnswer(HttpStatusCode.OK, answer.ToJsonString()), LensesAnswer = EmptyMap });
        page.AnchorText = Question;

        await page.Submit(Token);

        Assert.Equal(files.Take(25), page.AnswerFiles.Select(f => f.Path));
        Assert.Equal((true, "and 5 more files"), (page.HasAnswerFilesMore, page.AnswerFilesMore));
        Assert.Equal("The map draws nothing for this question, but the answer names 30 files. They are listed above.", page.StageMessage);
    }
}
