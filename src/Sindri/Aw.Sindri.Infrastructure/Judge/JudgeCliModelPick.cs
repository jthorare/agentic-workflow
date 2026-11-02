using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Judge;

/// <summary>
/// <see cref="IModelPickJudge"/> over the judge program (AW_SINDRI_JUDGE, else 'judge' on the PATH), run through <see cref="IToolRunner"/> as 'judge model-pick' with the question on standard input
/// (never an argument: a 100,000-character pack passes Linux's 131,072-byte limit for one argument; model-pick spec 4.2). No '--providers': the chain is fixed in the question (Jev, then rules), and a flag can only narrow it.
/// It runs with AW_JUDGE_STATE_DIR set to Sindri's own judge directory, like <see cref="JudgeCliChoice"/>. Exit 0 is a decided envelope and exit 2 an escalated one.
/// </summary>
/// <param name="runner">Runs the judge.</param>
/// <param name="environment">Reads AW_SINDRI_JUDGE.</param>
/// <param name="stateDirectory">The directory for the judge's own store (AW_JUDGE_STATE_DIR).</param>
internal sealed class JudgeCliModelPick(IToolRunner runner, Func<string, string?> environment, string stateDirectory) : IModelPickJudge
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(40);   // over the question's own 20 s provider budget

    public ValueTask<ChooseAnswer> Pick(ModelPickAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        cancellationToken.ThrowIfCancellationRequested();
        var program = environment("AW_SINDRI_JUDGE") is { Length: > 0 } given ? given : "judge";
        var options = new ToolRunOptions(Environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["AW_JUDGE_STATE_DIR"] = stateDirectory }, StandardInput: Input(ask));
        var run = runner.Run(program, ["model-pick"], _timeout, options, cancellationToken);
        return ValueTask.FromResult(JudgeCliChoice.Answer(run, program, "model-pick"));
    }

    private static string Input(ModelPickAsk ask)
    {
        var keys = ask.Candidates.ToDictionary(static c => c.Model, static c => c.Key, StringComparer.Ordinal);
        return new JsonObject
        {
            ["step"] = ask.Step,
            ["kind"] = ask.Kind,
            ["brief"] = ask.Brief,
            ["files"] = new JsonArray([.. ask.Files.Select(static f => (JsonNode)new JsonObject { ["path"] = f.Path, ["text"] = f.Text })]),
            ["candidates"] = new JsonArray([.. ask.Candidates.Select(static c => (JsonNode)new JsonObject { ["key"] = c.Key, ["model"] = c.Model, ["tier"] = c.Tier })]),
            ["denyPaths"] = new JsonArray([.. ask.DenyPaths.Select(static g => (JsonNode)JsonValue.Create(g))]),
            ["history"] = new JsonArray([.. ask.History.Where(h => keys.ContainsKey(h.Model)).Select(h => (JsonNode)new JsonObject
            {
                ["key"] = keys[h.Model],
                ["runs"] = h.Runs,
                ["refused"] = h.Refused,
                ["checkFailed"] = h.CheckFailed,
                ["redraft"] = h.Redraft,
            })]),
        }.ToJsonString();
    }
}
