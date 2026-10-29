using System.Globalization;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Models;
using Aw.Context.Infrastructure.Labeling;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Labeling;

/// <summary>Generate candidates, then Jev picks (spec 6.1 to 6.5).</summary>
[Trait("Lane", "unit")]
public sealed class ModelLabelerPickTests
{
    private static readonly LabelOptions _on = new(Pick: true, Candidates: 3);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FileCluster Cluster(int i, string? fileName = "src/tabs", params string[] files) =>
        new(ClusterId.From(string.Create(CultureInfo.InvariantCulture, $"h{i:D5}")), files.Length > 0 ? files : [string.Create(CultureInfo.InvariantCulture, $"src/f{i}/a.ts"), string.Create(CultureInfo.InvariantCulture, $"src/f{i}/b.ts")], 1.0) { FileName = fileName };

    private static string Id(int i) => "h" + i.ToString("D5", CultureInfo.InvariantCulture);

    private static string Labels(int i, params string[] labels) => $$"""{"id":"{{Id(i)}}","labels":[{{string.Join(',', labels.Select(l => '"' + l + '"'))}}]}""";

    private static FakeModelPort Model(params string[] entries) => new() { Answer = "[" + string.Join(',', entries) + "]" };

    private static ModelLabeler Labeler(IModelPort model, IChoicePort? choice) => new(model, new RedactionScrubberFactory(new FakeTimeProvider()), choice);

    private static FakeChoicePort Picking(string? picked, double confidence = 0.9) => new() { Script = qs => Results.Success<IReadOnlyList<ChoiceAnswer>>([.. qs.Select(q => new ChoiceAnswer(q.Key, picked, confidence))]) };

    [Fact]
    public async Task The_model_writes_candidates_the_file_name_joins_last_and_jev_picks_one()
    {
        var port = Picking("Tabs");

        var pass = await Labeler(Model(Labels(0, "Tab groups", "Tabs", "Tab manager")), port).Label([Cluster(0)], [], 1_000_000, _on, Ct);

        var asked = Assert.Single(Assert.Single(port.Asked).Questions);
        Assert.Equal(Id(0), asked.Key);
        Assert.Equal(["Tab groups", "Tabs", "Tab manager", "src/tabs"], asked.Options, StringComparer.Ordinal);
        Assert.Equal([(Id(0), "Tabs")], pass.Value.Labels.Select(l => (l.Id.Value, l.Label)));
        Assert.Equal(new LabelHow(Jev: 1, FirstCandidate: 0, FileName: 0), pass.Value.Chosen);
        Assert.Equal(ChoiceKind.Labeling, port.Asked[0].Kind);
    }

    [Fact]
    public async Task The_question_holds_the_scrubbed_paths_and_names_them_data()
    {
        var port = Picking("Tabs");

        _ = await Labeler(Model(Labels(0, "Tab groups", "Tabs")), port).Label([Cluster(0, "src/tabs", "src/tabs/TabManager.ts", "src/tabs/TabBar.ts")], [], 1_000_000, _on, Ct);

        var prompt = Assert.Single(Assert.Single(port.Asked).Questions).Prompt;
        Assert.Contains("src/tabs/TabManager.ts", prompt, StringComparison.Ordinal);
        Assert.Contains("data", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicates_are_dropped_case_insensitively_and_a_cluster_left_with_one_candidate_is_not_asked_about()
    {
        var port = Picking("whatever");

        var pass = await Labeler(Model(Labels(0, "Tabs", "tabs", "TABS")), port).Label([Cluster(0, fileName: "TaBs")], [], 1_000_000, _on, Ct);

        Assert.Empty(port.Asked);
        Assert.Equal([(Id(0), "Tabs")], pass.Value.Labels.Select(l => (l.Id.Value, l.Label)));
        Assert.Equal(new LabelHow(0, FirstCandidate: 1, 0), pass.Value.Chosen);
    }

    [Fact]
    public async Task A_cluster_the_model_gave_nothing_for_keeps_its_file_name_and_asks_nothing()
    {
        var port = Picking("whatever");

        var pass = await Labeler(Model(), port).Label([Cluster(0)], [], 1_000_000, _on, Ct);

        Assert.Empty(port.Asked);
        Assert.Empty(pass.Value.Labels);
        Assert.Equal(new LabelHow(0, 0, FileName: 1), pass.Value.Chosen);
    }

    [Theory]
    [InlineData(null, 0.9)]            // an answer that is none of the options
    [InlineData("Not offered", 0.9)]
    [InlineData("tabs", 0.9)]          // not exactly equal to an offered candidate
    [InlineData("Tabs", 0.49)]         // under the confidence bar
    public async Task An_undecided_cluster_takes_the_models_first_candidate(string? picked, double confidence)
    {
        var port = Picking(picked, confidence);

        var pass = await Labeler(Model(Labels(0, "Tab groups", "Tabs")), port).Label([Cluster(0)], [], 1_000_000, _on, Ct);

        Assert.Equal([(Id(0), "Tab groups")], pass.Value.Labels.Select(l => (l.Id.Value, l.Label)));
        Assert.Equal(new LabelHow(0, FirstCandidate: 1, 0), pass.Value.Chosen);
    }

    [Fact]
    public async Task Confidence_of_exactly_half_acts()
    {
        var pass = await Labeler(Model(Labels(0, "Tab groups", "Tabs")), Picking("Tabs", confidence: 0.5)).Label([Cluster(0)], [], 1_000_000, _on, Ct);

        Assert.Equal("Tabs", Assert.Single(pass.Value.Labels).Label);
    }

    [Fact]
    public async Task A_question_the_answer_leaves_out_is_undecided()
    {
        var port = new FakeChoicePort { Script = _ => Results.Success<IReadOnlyList<ChoiceAnswer>>([]) };

        var pass = await Labeler(Model(Labels(0, "Tab groups", "Tabs")), port).Label([Cluster(0)], [], 1_000_000, _on, Ct);

        Assert.Equal("Tab groups", Assert.Single(pass.Value.Labels).Label);
    }

    [Fact]
    public async Task A_candidate_is_cleaned_before_it_is_offered_and_before_it_is_chosen()
    {
        var port = Picking("Bad name");

        var pass = await Labeler(Model(Labels(0, "Bad\\u0007  name", "Other")), port).Label([Cluster(0)], [], 1_000_000, _on, Ct);

        Assert.Equal(["Bad name", "Other", "src/tabs"], Assert.Single(Assert.Single(port.Asked).Questions).Options);
        Assert.Equal("Bad name", Assert.Single(pass.Value.Labels).Label);
    }

    [Fact]
    public async Task A_secret_a_model_echoes_into_a_candidate_never_reaches_the_port()
    {
        var port = Picking("Tabs");

        _ = await Labeler(Model(Labels(0, "Tabs", $"Key {Planted.Secret}")), port).Label([Cluster(0, files: [$"src/{Planted.Secret}.ts"])], [], 1_000_000, _on, Ct);

        var question = Assert.Single(Assert.Single(port.Asked).Questions);
        Assert.DoesNotContain(Planted.Secret, question.Prompt + string.Concat(question.Options), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pick_packs_are_sized_by_the_ports_cap_in_cluster_id_order()
    {
        var port = new FakeChoicePort { Cap = 40_000 };
        var clusters = Enumerable.Range(0, 120).Select(i => Cluster(i, files: [.. Enumerable.Range(0, 12).Select(f => string.Create(CultureInfo.InvariantCulture, $"src/module{i}/feature/component{f}/{new string('x', 40)}.ts"))])).Reverse().ToList();
        var model = new FakeModelPort { Responder = r => "[" + string.Join(',', Enumerable.Range(0, 120).Where(i => r.Prompt.Contains(Id(i), StringComparison.Ordinal)).Select(i => Labels(i, "One", "Two"))) + "]" };

        _ = await Labeler(model, port).Label(clusters, [], 10_000_000, _on, Ct);

        Assert.True(port.Asked.Count > 1);
        Assert.All(port.Asked, a => Assert.True(a.Questions.Sum(q => q.Prompt.Length + q.Options.Sum(o => o.Length)) <= 32_000));
        Assert.All(port.Asked, a => Assert.Equal(40_000, a.CapChars));
        var keys = port.Asked.SelectMany(a => a.Questions).Select(q => q.Key).ToList();
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys, StringComparer.Ordinal);
        Assert.Equal(120, keys.Count);
    }

    [Fact]
    public async Task A_cap_too_small_for_one_question_skips_the_pick_and_every_cluster_takes_its_first_candidate()
    {
        var port = new FakeChoicePort { Cap = 1_000 };
        var files = Enumerable.Range(0, 12).Select(f => string.Create(CultureInfo.InvariantCulture, $"src/module/feature/component{f}/{new string('x', 40)}.ts")).ToArray();   // a question of about 900 characters, over 80% of 1,000

        var pass = await Labeler(Model(Labels(0, "Tab groups", "Tabs")), port).Label([Cluster(0, files: files)], [], 1_000_000, _on, Ct);

        Assert.Empty(port.Asked);
        Assert.Equal("Tab groups", Assert.Single(pass.Value.Labels).Label);
        Assert.Equal(new LabelHow(0, FirstCandidate: 1, 0), pass.Value.Chosen);
    }

    [Fact]
    public async Task With_the_pick_off_the_port_is_never_asked()
    {
        var port = Picking("Tabs");
        var model = new FakeModelPort { Answer = """[{"id":"h00000","label":"Alpha"}]""" };

        var pass = await Labeler(model, port).Label([Cluster(0)], [], 1_000_000, new LabelOptions(Pick: false, Candidates: 3), Ct);

        Assert.Empty(port.Asked);
        Assert.Equal("Alpha", Assert.Single(pass.Value.Labels).Label);
        Assert.Null(pass.Value.Chosen);
    }

    [Fact]
    public async Task How_each_cluster_was_chosen_adds_up_to_the_clusters()
    {
        var port = new FakeChoicePort
        {
            Script = qs => Results.Success<IReadOnlyList<ChoiceAnswer>>([.. qs.Where(q => q.Key == Id(0)).Select(q => new ChoiceAnswer(q.Key, "Tabs", 0.9))]),
        };

        var pass = await Labeler(Model(Labels(0, "Tab groups", "Tabs"), Labels(1, "Panes", "Layout")), port).Label([Cluster(0), Cluster(1), Cluster(2)], [], 1_000_000, _on, Ct);

        Assert.Equal(new LabelHow(Jev: 1, FirstCandidate: 1, FileName: 1), pass.Value.Chosen);
        Assert.Equal([(Id(0), "Tabs"), (Id(1), "Panes")], pass.Value.Labels.Select(l => (l.Id.Value, l.Label)));
    }

    [Fact]
    public async Task Jev_tokens_count_toward_the_cap_and_a_pick_that_would_pass_it_is_skipped()
    {
        var files = Enumerable.Range(0, 12).Select(f => string.Create(CultureInfo.InvariantCulture, $"src/module/feature/component{f}/{new string('x', 40)}.ts")).ToArray();
        var probe = Model(Labels(0, "Tab groups", "Tabs"));
        _ = await Labeler(probe, Picking("Tabs")).Label([Cluster(0, files: files)], [], 1_000_000, _on, Ct);
        var request = Assert.Single(probe.Requests);
        var generateAlone = ((request.Prompt.Length + 3) / 4) + request.MaxTokens;   // what the generate call may cost; the question's characters / 4 come on top
        var tight = Picking("Tabs");

        var pass = await Labeler(Model(Labels(0, "Tab groups", "Tabs")), tight).Label([Cluster(0, files: files)], [], generateAlone, _on, Ct);

        Assert.Empty(tight.Asked);
        Assert.True(pass.IsSuccess);
        Assert.Equal("Tab groups", Assert.Single(pass.Value.Labels).Label);
        Assert.Equal("token-cap", pass.Value.PickUnavailable);
    }
}
