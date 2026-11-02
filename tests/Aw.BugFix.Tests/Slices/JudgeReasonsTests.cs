using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Features.Resume;
using Aw.BugFix.Features.Status;
using Aw.BugFix.Host;
using Aw.BugFix.Testing;
using Aw.Kernel.Json;

namespace Aw.BugFix.Tests.Slices;

/// <summary>The state keeps judge's reasons with its verdict, so a later session sees why a candidate wasn't resolved (#1248).</summary>
public sealed class JudgeReasonsTests : IDisposable
{
    private const string Reason = "the redirect still drops the cookie";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(BugFixJsonContext.Default);

    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task The_recorded_verdict_keeps_the_judges_reasons_in_state_json()
    {
        await Judged(JudgeDecision.Partial);

        var verdict = StateVerdict(StateJson());

        Assert.True(verdict.TryGetPropertyValue("reasons", out var reasons), "state.json candidates[0].verdict has no reasons");
        Assert.Equal([Reason], [.. reasons!.AsArray().Select(static r => r!.GetValue<string>())]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_and_resume_show_the_reasons_of_the_recorded_verdict()
    {
        await Judged(JudgeDecision.Partial);

        var status = World.Ok(await _w.Run<StatusRequest, BugFixState>(new StatusRequest(World.State)));
        var resume = World.Ok(await _w.Run<ResumeRequest, ResumeResponse>(new ResumeRequest(World.State)));

        Assert.Contains(Reason, JsonSerializer.Serialize(status, _json), StringComparison.Ordinal);
        Assert.Contains(Reason, JsonSerializer.Serialize(resume, _json), StringComparison.Ordinal);
    }

    // A guard for the fix: a state file written before the field existed must keep reading, never as an error.
    [Fact, Trait("Lane", "unit")]
    public async Task A_state_file_without_reasons_still_reads()
    {
        await Judged(JudgeDecision.Resolved);
        var old = StateJson();
        _ = StateVerdict(old).Remove("reasons");

        var read = JsonSerializer.Deserialize<BugFixState>(old.ToJsonString(), _json);

        Assert.Equal(JudgeDecision.Resolved, read?.Candidates[0].Verdict?.Decision);
    }

    private async Task Judged(JudgeDecision decision)
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = _w.Judge.Answers(A.Verdict(decision) with { Reasons = [Reason] });
        _ = World.Ok(await _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, c1)));
    }

    private JsonNode StateJson() => JsonNode.Parse(JsonSerializer.Serialize(_w.Now, _json))!;

    private static JsonObject StateVerdict(JsonNode state) => state["candidates"]![0]!["verdict"]!.AsObject();
}
