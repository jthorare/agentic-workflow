using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The list row (E-20): every field of it, the rows the engine builds as golden files, the 'now' of every API golden row against its chip's pattern, and the time the caller reads at.</summary>
[Lane(TestLane.Unit)]
public sealed partial class InstanceSummaryRowTests
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true, NewLine = "\n" };

    [Fact]
    public void Every_field_of_a_row_is_the_views_or_the_composers()
    {
        var view = StatusCases.View("desk-task");

        var row = InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now));

        Assert.Equal(view.Address, row.Address);
        Assert.Equal(view.Id, row.Id);
        Assert.Equal(view.ItemId, row.ItemId);
        Assert.Equal(new InstanceRecipe(view.Recipe.Id, view.Recipe.Hash, view.Recipe.Version, view.Urgency, DisplayName: null), row.Recipe);
        Assert.Equal(view.State, row.State);
        Assert.Equal(new StateChip("DECISION", ChipTone.Attn), row.Chip);
        Assert.Equal(view.Since, row.Since);
        Assert.Equal(view.EngineOwned, row.EngineOwned);
        Assert.Equal(new SubjectRef("text", "fact:spec", "spec.md", Href: null), row.Subject);
        Assert.Same(view.Inputs, row.Inputs);
        Assert.Same(view.States, row.States);
        Assert.Equal("needs your output (desk answer)", row.Now);
        Assert.Equal(view.AnswerLine, row.Answer);
        Assert.Equal([ActionId.From("CompleteStep"), ActionId.From("ApplyCommand:takeover"), ActionId.From("ApplyCommand:cancel")], row.Actions);
    }

    [Fact]
    public void The_actions_of_a_row_are_the_action_ids_of_the_status_commands_and_the_rerun_for_every_case()
    {
        foreach (var name in StatusCases.Names)
        {
            var view = StatusCases.View(name);

            Assert.Equal(InstanceViews.Status(view, StatusCases.Now).Commands.Select(static c => c.ActionId).Concat(Reruns.Of(view)), InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now)).Actions);   // the rerun is the one action the status does not list
        }
    }

    [Fact]
    public void An_unavailable_park_waits_on_the_engine_not_on_you_and_says_so()
    {
        var row = InstanceSummaries.Of(StatusCases.View("parked-unavailable-step"), new InstanceSummaryContext(StatusCases.Now));

        Assert.Equal(("waiting on a dependency that is down", WaitingOn.Engine), (row.Now, row.WaitingOn));
        Assert.Equal(AnswerKind.Resume, row.Answer?.Kind);   // the resume is still there to run
    }

    [Fact]
    public void An_answer_the_park_takes_is_on_the_row()
    {
        var row = InstanceSummaries.Of(StatusCases.View("parked-needs-answer-step"), new InstanceSummaryContext(StatusCases.Now));

        Assert.Equal(AnswerKind.Text, row.Answer?.Kind);
        Assert.Contains(ActionId.From("ApplyCommand:cancel"), row.Actions);
    }

    [Fact]
    public void A_row_for_an_instance_with_no_input_is_about_its_item_titled_when_the_caller_knows_it_else_labelled_by_its_address()
    {
        var view = StatusCases.View("desk-task") with { Subject = null, ItemId = ItemKey.From("T-3") };

        Assert.Equal(new SubjectRef("item", "T-3", "Crash on launch", Href: null), InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now, ItemTitle: "Crash on launch")).Subject);
        Assert.Equal(new SubjectRef("item", "T-3", view.Address.Value, Href: null), InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now)).Subject);
    }

    [Fact]
    public void A_row_for_an_item_subject_shows_the_title_the_caller_knows_and_the_id_when_it_knows_none()
    {
        var view = StatusCases.View("desk-task") with { Subject = new SubjectRef("item", "T-3", "T-3", Href: null) };

        Assert.Equal("Crash on launch", InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now, ItemTitle: "Crash on launch")).Subject.Label);
        Assert.Equal("T-3", InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now)).Subject.Label);
    }

    [Fact]
    public void A_title_is_not_a_label_for_a_subject_that_is_not_the_item()
    {
        var view = StatusCases.View("desk-task") with { Subject = new SubjectRef("file", "spec.md", "spec.md", Href: null) };

        Assert.Equal("spec.md", InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now, ItemTitle: "Crash on launch")).Subject.Label);
    }

    [Fact]
    public void The_detail_labels_an_item_subject_with_its_title()
    {
        var view = StatusCases.View("desk-task") with { Subject = new SubjectRef("item", "T-3", "T-3", Href: null) };

        var detail = InstanceViews.Detail(view, new InstanceDetailContext("Crash on launch", Parent: null, new ViewCounts(0, 0, 0, Context: null), StatusCases.Now));

        Assert.Equal(new SubjectRef("item", "T-3", "Crash on launch", Href: null), detail.Subject);
    }

    [Fact]
    public void The_status_ages_and_timeout_move_with_the_time_the_caller_reads_at_and_the_view_does_not()
    {
        var view = StatusCases.View("desk-task");
        var later = StatusCases.Now + TimeSpan.FromMinutes(10);

        var first = InstanceViews.Status(view, StatusCases.Now);
        var second = InstanceViews.Status(view, later);

        Assert.Equal(first.Header.AgeSeconds + 600, second.Header.AgeSeconds);
        Assert.Equal(first.Timeout!.InSeconds - 600, second.Timeout!.InSeconds);
        Assert.NotEqual(first.Text, second.Text, StringComparer.Ordinal);
        Assert.Contains("(1h 22m)", second.Text, StringComparison.Ordinal);
        Assert.Contains("parks in 1d 22h 38m", second.Text, StringComparison.Ordinal);
        Assert.Equal((first.Budget, first.Next, first.Inputs.Count), (second.Budget, second.Next, second.Inputs.Count));
    }

    [Fact]
    public void The_detail_is_as_of_the_time_in_its_context_not_the_time_the_view_was_built()
    {
        var view = StatusCases.View("desk-task");
        var counts = new ViewCounts(0, 0, 0, Context: null);

        var early = InstanceViews.Detail(view, new InstanceDetailContext("t", Parent: null, counts, StatusCases.Now));
        var late = InstanceViews.Detail(view, new InstanceDetailContext("t", Parent: null, counts, StatusCases.Now + TimeSpan.FromHours(1)));

        Assert.Equal(3600, late.Current.Status.Header.AgeSeconds - early.Current.Status.Header.AgeSeconds);
        Assert.Equal(3600, early.Current.Status.Timeout!.InSeconds - late.Current.Status.Timeout!.InSeconds);
        Assert.Equal(StatusCases.Now + TimeSpan.FromHours(1), late.AsOf);
    }

    [Theory]
    [InlineData("desk-task")]
    [InlineData("taken-over")]
    [InlineData("parked-needs-answer-step")]
    [InlineData("parked-unavailable-step")]
    [InlineData("escalated-gate")]
    [InlineData("done")]
    [InlineData("failed")]
    [InlineData("unresolved")]
    [InlineData("cancelled")]
    [InlineData("escalated-child")]
    [InlineData("parked-mirrored-budget")]
    public void The_built_row_serializes_to_its_golden_file(string name)
    {
        var row = InstanceSummaries.Of(StatusCases.View(name), new InstanceSummaryContext(StatusCases.Now));

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(row, _options));
        Golden.Match(JsonSerializer.Serialize(doc.RootElement, _indented) + "\n", typeof(InstanceSummaryRowTests).Assembly, $"Layout/Golden/instance-summary-{name}.json", Normalization.None);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("open")]
    [InlineData("recipe-filter")]
    public void Every_now_in_an_api_golden_list_follows_the_pattern_of_its_chip(string scenario)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Golden.SourcePath(typeof(InstanceSummaryRowTests).Assembly, $"Golden/Api/workflow-instances/{scenario}.json")));

        var rows = doc.RootElement.GetProperty("instances").EnumerateArray().Select(static r => (Chip: r.GetProperty("chip").GetProperty("word").GetString()!, Now: r.GetProperty("now").GetString()!)).ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(Pattern(row.Chip).IsMatch(row.Now), $"{row.Chip}: '{row.Now}'"));
    }

    [Theory]
    [InlineData("RUNNING", "engine polling in ITM-01JKH", false)]
    [InlineData("RUNNING", "engine waiting in ITM-01JKH", true)]
    [InlineData("RUNNING", "agent rolling back in ITM-01JKH", true)]
    [InlineData("RUNNING", "agent producing in T-3/step/1", true)]
    [InlineData("RUNNING", "agent produce in ITM-01JKH", false)]
    [InlineData("ESCALATED", "parked (rounds)", false)]
    [InlineData("ESCALATED", "escalated (rounds)", true)]
    [InlineData("ESCALATED", "escalated", true)]
    [InlineData("PARKED", "parked (rounds)", true)]
    [InlineData("PARKED", "parked", true)]
    [InlineData("PARKED", "needs your answer", true)]
    [InlineData("PARKED", "escalated (rounds)", false)]
    public void The_chip_pattern_accepts_what_row_sentences_builds_and_nothing_else(string chip, string now, bool accepted) =>
        Assert.Equal(accepted, Pattern(chip).IsMatch(now));

    private static Regex Pattern(string chip) => chip switch
    {
        "DECISION" => DecisionNow,
        "PARKED" or "ESCALATED" => ParkNow(chip),
        "WITH YOU" => WithYouNow,
        "RUNNING" => RunningNow,
        "QUEUED" => QueuedNow,
        "DONE" => DoneNow,
        _ => throw new ArgumentOutOfRangeException(nameof(chip), chip, "no such chip"),
    };

    [GeneratedRegex(@"^needs your [a-z-]+ \(desk answer\)\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DecisionNow { get; }

    /// <summary>What RowSentences builds under PARKED or ESCALATED: the answer line's sentence, else the chip's own word, lowercased, with the detail in brackets.</summary>
    private static Regex ParkNow(string chip) =>
        new($@"^(needs your answer|{Regex.Escape(chip.ToLowerInvariant())}( \(.+\))?)\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"^with you: .+\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WithYouNow { get; }

    [GeneratedRegex(@"^(engine (waiting|running)|agent [a-z]+ing( [a-z]+)*) in (ITM-[0-9A-Za-z]+|\S+/\S+/[0-9]+)\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RunningNow { get; }

    [GeneratedRegex(@"^queued\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QueuedNow { get; }

    [GeneratedRegex(@"^[a-z-]+\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DoneNow { get; }
}
