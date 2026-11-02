using Aw.Context.Core.ConceptScan;

namespace Aw.Context.Tests.ConceptScan;

public sealed class ConceptScanRepliesTests
{
    private static string Invariant(FormattableString text) => text.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static readonly HashSet<string> _features = new(["feature:shifts.swap", "feature:shifts.open", "feature:pay.run", "feature:pay.slip"], StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Candidates_read_a_concepts_object()
    {
        var found = ConceptScanReplies.Candidates("""{"concepts":[{"name":"Self scheduling","queries":["shift swap","open shifts"]},{"name":"Payroll","queries":["pay run"]}]}""");

        Assert.Equal(["Self scheduling", "Payroll"], found.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.Equal(["shift swap", "open shifts"], found[0].Queries, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Candidates_read_a_bare_array_inside_a_code_fence_and_words()
    {
        var found = ConceptScanReplies.Candidates("Here you go:\n```json\n[{\"name\":\"Payroll\",\"queries\":[\"pay run\"]}]\n```\nHope that helps.");

        Assert.Equal(["Payroll"], found.Select(static c => c.Name), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("sure, here are some concepts")]
    [InlineData("{\"concepts\": 3}")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"concepts\":[{\"name\":\"Payroll\"")]
    public void A_reply_that_is_not_a_concept_list_finds_nothing(string reply)
    {
        Assert.Empty(ConceptScanReplies.Candidates(reply));
        Assert.Empty(ConceptScanReplies.Hints(reply, _features));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_candidate_needs_a_name_and_a_query_and_a_name_is_kept_once()
    {
        var found = ConceptScanReplies.Candidates("{\"concepts\":[{\"name\":\"\",\"queries\":[\"a\"]},{\"name\":\"No queries\",\"queries\":[]},{\"name\":\"Payroll\",\"queries\":[\"pay run\"]},{\"name\":\"payroll\",\"queries\":[\"other\"]},{\"queries\":[\"x\"]},{\"name\":7,\"queries\":[\"x\"]}]}");

        Assert.Equal(["Payroll"], found.Select(static c => c.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_is_one_plain_line_and_a_long_one_is_dropped()
    {
        var found = ConceptScanReplies.Candidates("{\"concepts\":[{\"name\":\"Self\\n  scheduling\\t\",\"queries\":[\"swap\"]},{\"name\":\"" + new string('x', 61) + "\",\"queries\":[\"swap\"]}]}");

        Assert.Equal(["Self scheduling"], found.Select(static c => c.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void At_most_three_queries_per_candidate_and_every_concept()
    {
        var concepts = string.Join(',', Enumerable.Range(1, 40).Select(static i => Invariant($"{{\"name\":\"Concept {i}\",\"queries\":[\"a\",\"b\",\"c\",\"d\"]}}")));

        var found = ConceptScanReplies.Candidates("{\"concepts\":[" + concepts + "]}");

        Assert.Equal(40, found.Count);
        Assert.All(found, static c => Assert.Equal(3, c.Queries.Count));
        Assert.Equal("Concept 1", found[0].Name);
    }

    [Fact, Trait("Lane", "unit")]
    public void Hints_keep_the_members_that_are_feature_keys_and_drop_the_rest()
    {
        var hints = ConceptScanReplies.Hints("{\"concepts\":[{\"name\":\"Self scheduling\",\"members\":[\"feature:shifts.swap\",\"feature:shifts.open\",\"feature:made.up\",\"code:src/a.ts\",\"feature:shifts.swap\"],\"evidence\":[\"src/shifts/swap.ts\"],\"confidence\":0.8}]}", _features);

        var hint = Assert.Single(hints);
        Assert.Equal("Self scheduling", hint.Name);
        Assert.Equal(["feature:shifts.swap", "feature:shifts.open"], hint.Members, StringComparer.Ordinal);
        Assert.Equal(["src/shifts/swap.ts"], hint.Evidence, StringComparer.Ordinal);
        Assert.Equal(0.8, hint.Confidence);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hint_left_with_no_member_is_dropped()
    {
        var hints = ConceptScanReplies.Hints("""{"concepts":[{"name":"Ghost","members":["feature:made.up"],"confidence":0.9},{"name":"Empty","members":[]}]}""", _features);

        Assert.Empty(hints);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_stays_with_the_hint_of_the_higher_confidence()
    {
        var hints = ConceptScanReplies.Hints("{\"concepts\":[ {\"name\":\"Weak\",\"members\":[\"feature:pay.run\",\"feature:pay.slip\"],\"confidence\":0.4}, {\"name\":\"Strong\",\"members\":[\"feature:pay.run\"],\"confidence\":0.9}]}", _features);

        Assert.Equal(["Strong", "Weak"], hints.Select(static h => h.Name), StringComparer.Ordinal);
        Assert.Equal(["feature:pay.run"], hints[0].Members, StringComparer.Ordinal);
        Assert.Equal(["feature:pay.slip"], hints[1].Members, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Confidence_is_clamped_to_zero_to_one_and_a_missing_one_is_half()
    {
        var hints = ConceptScanReplies.Hints("{\"concepts\":[{\"name\":\"High\",\"members\":[\"feature:pay.run\"],\"confidence\":7},{\"name\":\"Low\",\"members\":[\"feature:pay.slip\"],\"confidence\":-2},{\"name\":\"None\",\"members\":[\"feature:shifts.open\"]},{\"name\":\"Text\",\"members\":[\"feature:shifts.swap\"],\"confidence\":\"high\"}]}", _features);

        Assert.Equal(1.0, hints.Single(static h => h.Name == "High").Confidence);
        Assert.Equal(0.0, hints.Single(static h => h.Name == "Low").Confidence);
        Assert.Equal(0.5, hints.Single(static h => h.Name == "None").Confidence);
        Assert.Equal(0.5, hints.Single(static h => h.Name == "Text").Confidence);
    }

    [Fact, Trait("Lane", "unit")]
    public void Forty_concepts_are_all_kept_most_confident_first()
    {
        var keys = new HashSet<string>(Enumerable.Range(1, 40).Select(static i => Invariant($"feature:f{i}")), StringComparer.Ordinal);
        var concepts = string.Join(',', Enumerable.Range(1, 40).Select(static i => Invariant($"{{\"name\":\"Concept {i}\",\"members\":[\"feature:f{i}\"],\"confidence\":{i / 100.0}}}")));

        var hints = ConceptScanReplies.Hints("{\"concepts\":[" + concepts + "]}", keys);

        Assert.Equal(40, hints.Count);
        Assert.Equal("Concept 40", hints[0].Name);
        Assert.Contains(hints, static h => h.Name == "Concept 5");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hints_reply_cut_off_after_three_whole_hints_keeps_the_three()
    {
        var cut = "{\"concepts\":[{\"name\":\"A\",\"members\":[\"feature:pay.run\"],\"confidence\":0.9},{\"name\":\"B\",\"members\":[\"feature:pay.slip\"],\"confidence\":0.8},{\"name\":\"C\",\"members\":[\"feature:shifts.open\"],\"confidence\":0.7},{\"name\":\"D\",\"members\":[\"feat";

        var hints = ConceptScanReplies.Hints(cut, _features);

        Assert.Equal(["A", "B", "C"], hints.Select(static h => h.Name), StringComparer.Ordinal);
        Assert.True(ConceptScanReplies.IsCutOff(cut));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_candidates_reply_cut_off_mid_array_keeps_the_whole_candidates()
    {
        var cut = "```json\n{\"concepts\":[{\"name\":\"Payroll\",\"queries\":[\"pay run\"]},{\"name\":\"Self sched";

        var found = ConceptScanReplies.Candidates(cut);

        Assert.Equal(["Payroll"], found.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.True(ConceptScanReplies.IsCutOff(cut));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("sure, here are some concepts")]
    [InlineData("{\"concepts\":[{\"name\":\"Pay")]
    [InlineData("""{"concepts":[{"name":"Payroll","queries":["pay run"]}]}""")]
    public void A_reply_that_reads_whole_or_not_at_all_is_not_cut_off(string reply) =>
        Assert.False(ConceptScanReplies.IsCutOff(reply));
}
