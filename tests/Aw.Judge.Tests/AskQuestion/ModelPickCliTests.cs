using Aw.Judge.Tests.Questions;

namespace Aw.Judge.Tests.AskQuestion;

// 'judge model-pick < input.json': the input rides on stdin, so a pack past the 131,072-byte argument limit still works
// (docs/csharp/judge.md); with no Jev key the rules fallback answers the cheapest key.
public sealed class ModelPickCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_130000_character_input_on_stdin_is_answered_end_to_end()
    {
        var files = new[]
        {
            ("docs/guide.md", ModelPickJson.Lines(30_000)),
            ("src/a.cs", ModelPickJson.Lines(45_000)),
            ("src/b.cs", ModelPickJson.Lines(45_000)),
            ("docs/z.md", ModelPickJson.Lines(10_000)),
        };
        var input = ModelPickJson.Input(files);
        Assert.True(input.Length > 130_000, $"the input is {input.Length} characters");

        var run = await new JudgeCli(_state).RunWithInput(input, "model-pick");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var json = run.Json();
        Assert.Equal(("m1", "rules"), (json.GetProperty("decision").GetString(), json.GetProperty("model").GetString()));
        var why = await new JudgeCli(_state).Run("why", json.GetProperty("id").GetString()!);
        Assert.Equal(0, why.ExitCode);
        Assert.DoesNotContain(new string('x', 99), why.Stdout, StringComparison.Ordinal);   // why: the stored decision holds no file text
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_sample_input_runs_by_hand_and_prints_a_key()
    {
        var sample = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "JudgeFixtures", "model-pick-input.json"), TestContext.Current.CancellationToken);

        var run = await new JudgeCli(_state).RunWithInput(sample, "model-pick");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        Assert.Matches("^m[1-3]$", run.Json().GetProperty("decision").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_input_with_no_candidates_exits_2_with_the_invalid_input_escalation()
    {
        var run = await new JudgeCli(_state).RunWithInput(ModelPickJson.Input(candidates: 0), "model-pick");

        Assert.Equal(2, run.ExitCode);
        Assert.Equal("invalid-input", run.Json().GetProperty("reasonCode").GetString());
    }

    public void Dispose() => _state.Dispose();
}
