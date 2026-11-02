using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// The scoping answer log (#1205): <c language="text">step.interview.record</c> logs every answer of a round, not only the last, and the
/// log's entries pass SCP-ACC-003 when the scope cites them, with no one writing <c language="text">recordedAs</c> by hand.
/// </summary>
public sealed class ScopingAnswerLogTests
{
    private const string Round = """[{"question":"Q1","pick":"Q1.a"},{"question":"Q2","pick":"Q2.a"},{"question":"Q3","pick":"Q3.a"}]""";

    private static async Task<JsonArray> Record(string answers)
    {
        var facts = new ScopingFacts { ["answers"] = answers, ["answerLog"] = "[]" };
        var response = (await new ScopingDeskService().Handle(new ScopingDeskRequest("step.interview.record", facts), CancellationToken.None)).Value;
        return JsonNode.Parse(response.Outputs["answerLog"])!.AsArray();
    }

    private static async Task<ScopingCheckResponse> Evidence(ScopingFacts facts) =>
        (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), CancellationToken.None)).Value;

    private static ScopingFacts GoldWith(JsonArray log)
    {
        var facts = ScopingFacts.Expiry();
        facts["answerLog"] = log.ToJsonString();
        return facts;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_round_of_three_answers_logs_three_entries_with_their_question_and_pick()
    {
        var log = await Record(Round);

        Assert.Equal(3, log.Count);
        Assert.Equal(["Q1", "Q2", "Q3"], log.Select(e => e!["question"]?.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(["Q1.a", "Q2.a", "Q3.a"], log.Select(e => e!["pick"]?.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Answers_the_scope_cites_by_answer_id_pass_SCP_ACC_003_without_a_recordedAs()
    {
        // The gold acceptance items cite A1 and A2 (and nothing cites A3, so the log is cut to those two).
        var log = await Record(Round);
        var cited = new JsonArray(log.Take(2).Select(static e => e!.DeepClone()).ToArray());

        var response = await Evidence(GoldWith(cited));

        Assert.DoesNotContain(response.Findings, static f => f.Rule == "SCP-ACC-003");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_answer_nothing_cites_still_fails_SCP_ACC_003()
    {
        var log = await Record(Round);

        var response = await Evidence(GoldWith(log));

        Assert.Contains(response.Findings, static f => f.Rule == "SCP-ACC-003" && string.Equals(f.Item, "A3", StringComparison.Ordinal));
        Assert.DoesNotContain(response.Findings, static f => f.Rule == "SCP-ACC-003" && string.Equals(f.Item, "A1", StringComparison.Ordinal));
    }

    private static async Task<ScopingCheckResponse> RecordedAs(string question, string recordedAs)
    {
        var facts = ScopingFacts.Expiry();
        facts["answerLog"] = $$"""[{"id":"A1","question":"{{question}}","pick":"{{question}}.a","recordedAs":["{{recordedAs}}"],"digest":"d1"}]""";
        facts["questions"] = """[{"id":"Q9"}]""";   // why: a later round overwrote fact:questions, so Q1 is no longer in it
        return await Evidence(facts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_answer_recorded_as_an_earlier_rounds_question_passes_SCP_ACC_003()
    {
        var response = await RecordedAs("Q1", "Q1");

        Assert.DoesNotContain(response.Findings, static f => f.Rule == "SCP-ACC-003" && string.Equals(f.Item, "A1", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_answer_recorded_as_a_question_no_round_asked_still_fails_SCP_ACC_003()
    {
        var response = await RecordedAs("Q1", "Q99");

        Assert.Contains(response.Findings, static f => f.Rule == "SCP-ACC-003" && string.Equals(f.Item, "A1", StringComparison.Ordinal) && f.Message.Contains("recorded as Q99, which does not exist", StringComparison.Ordinal));
    }
}
