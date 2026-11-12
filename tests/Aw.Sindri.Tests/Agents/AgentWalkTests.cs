using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Agents;

/// <summary>The Agents page spec §4.3 walk (done-checks D1 and D2): the fixture's lanes give the reasons the spec lists, in the order the spec lists, and the text golden pins them.</summary>
public sealed class AgentWalkTests
{
    private static string Names(AgentRowList list) => string.Join(", ", list.Rows.Select(static r => r.Facts.Lane));

    [Fact, Trait("Lane", "unit")]
    public void The_fixture_has_the_eleven_lanes_of_the_walk()
    {
        Assert.Equal(["context", "design", "ideas", "ux", "meta", "ui", "qa", "engine", "tools", "perf", "api"], AgentWalk.Lanes.Select(static l => l["lane"]!.GetValue<string>()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_two_lanes_at_the_mark_are_past_the_recycle_size_the_fixture_names()
    {
        var recycleAt = AgentWalk.Doc["settings"]!["recycleAt"]!.GetValue<int>();

        foreach (var lane in AgentWalk.Lanes)
        {
            var past = lane["contextTokens"]!.GetValue<int>() >= recycleAt;
            Assert.Equal(past, lane["atRecycleMark"]?.GetValue<bool>() ?? false);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void At_09_10_only_ideas_meta_and_qa_hold_a_reason_and_each_has_the_spec_words()
    {
        foreach (var lane in AgentWalk.Lanes)
        {
            var name = lane["lane"]!.GetValue<string>();

            var reasons = AgentReasons.Evaluate(AgentWalk.Inputs(lane, AgentWalk.At, asked: false));

            Assert.Equal(AgentWalk.Expected("at", name), reasons.Select(AgentWalk.Line));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void At_09_10_the_rows_are_ordered_ideas_meta_qa_then_working_then_idle()
    {
        var list = AgentWalk.Rows(AgentWalk.At, asked: false);

        Assert.Equal(AgentWalk.Doc["expect"]!["rowOrder"]!.AsArray().Select(static n => n!.GetValue<string>()), list.Rows.Select(static r => r.Facts.Lane!));
        Assert.Equal(AgentWalk.Doc["expect"]!["needsYouOrder"]!.AsArray().Select(static n => n!.GetValue<string>()), list.Rows.Take(3).Select(static r => r.Facts.Lane!));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_head_counts_eleven_agents_three_that_need_you_and_two_PRs_ready()
    {
        var list = AgentWalk.Rows(AgentWalk.At, asked: false);
        var head = AgentWalk.Doc["expect"]!["head"]!;

        var needYou = list.Rows.Count(static r => r.Reasons.Count > 0 && r.Reasons[0].Class is not (2 or 6));
        var prs = list.Rows.SelectMany(static r => r.Reasons).Where(static r => r.Kind == AgentReasonKind.PrReady).Count();

        Assert.Equal((head["agents"]!.GetValue<int>(), head["needYou"]!.GetValue<int>(), head["prsReady"]!.GetValue<int>()), (list.Rows.Count, needYou, prs));
    }

    [Fact, Trait("Lane", "unit")]
    public void When_qa_asks_at_09_12_03_its_reason_rises_from_class_7_to_class_1_and_the_others_stay()
    {
        foreach (var name in (string[])["qa", "ideas", "meta"])
        {
            var lane = AgentWalk.Lanes.Single(l => l["lane"]!.GetValue<string>() == name);

            var reasons = AgentReasons.Evaluate(AgentWalk.Inputs(lane, AgentWalk.EventAt.AddSeconds(1), asked: true));

            Assert.Equal(AgentWalk.Expected("afterQaAsks", name), reasons.Select(AgentWalk.Line));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void After_qa_asks_and_the_owner_re_ranks_the_order_is_qa_ideas_meta_and_qa_shows_the_diamond()
    {
        var list = AgentWalk.Rows(AgentWalk.EventAt.AddSeconds(1), asked: true);

        Assert.Equal(AgentWalk.Doc["expect"]!["needsYouOrderAfterQaAsks"]!.AsArray().Select(static n => n!.GetValue<string>()), list.Rows.Take(3).Select(static r => r.Facts.Lane!));
        Assert.Equal(AgentState.WaitingOnYou, list.Rows[0].State);
        Assert.Equal(11, list.Rows.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_row_of_the_walk_has_a_unique_session_id_and_one_primary_action()
    {
        var list = AgentWalk.Rows(AgentWalk.At, asked: false);

        Assert.Equal(list.Rows.Count, list.Rows.Select(static r => r.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(list.Rows, static r => Assert.NotEmpty(r.Actions));
        Assert.StartsWith("ideas", Names(list), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_reasons_of_the_walk_match_their_golden()
    {
        var text = new System.Text.StringBuilder();
        foreach (var (title, now, asked) in ((string, DateTimeOffset, bool)[])[("09:10:00", AgentWalk.At, false), ("09:12:04", AgentWalk.EventAt.AddSeconds(1), true)])
        {
            _ = text.Append("# ").Append(title).Append('\n');
            foreach (var row in AgentWalk.Rows(now, asked).Rows)
            {
                _ = text.Append(row.Facts.Lane).Append(": ").Append(row.Reasons.Count == 0 ? "-" : string.Join(" | ", row.Reasons.Select(AgentWalk.Line))).Append('\n');
            }
        }

        Golden.Match(text.ToString(), typeof(AgentWalkTests).Assembly, "Golden/Agents/walk-reasons.txt", Normalization.None);
    }
}
