using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Features.SortPrompt;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.SortPrompt;

/// <summary>The SortPrompt service over in-memory fakes: a decision log, a sorter store, a session store, a config store and a scripted Jev.</summary>
internal sealed class SortPromptHarness
{
    public SortPromptHarness(FakeBatchClassifier? jev = null, InMemorySortSessionStore? sessions = null)
    {
        Jev = jev ?? FakeBatchClassifier.Unavailable();
        Sessions = sessions ?? new InMemorySortSessionStore();
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var recorder = new SortRecorder(Log, Store, new FakeInputRedactor(), new SequentialIdGenerator(), clock, AwJson.CreateOptions(JudgeJsonContext.Default));
        Service = new SortPromptService(Config, Jev, Sessions, recorder, clock);
    }

    public InMemoryDecisionLog Log { get; } = new();

    public InMemoryPromptSortStore Store { get; } = new();

    public InMemorySortSessionStore Sessions { get; }

    public InMemoryJudgeConfigStore Config { get; } = new();

    public FakeBatchClassifier Jev { get; }

    public SortPromptService Service { get; }

    /// <summary>Turns scaffolds on in the stored config.</summary>
    public void SwitchOn(params string[] scaffolds) =>
        Config.Stored = JudgeConfig.Default with
        {
            PromptSort = new PromptSortConfig(Scaffolds: scaffolds.ToDictionary(static s => s, static _ => true, StringComparer.Ordinal)),
        };

    /// <summary>Jev answers decisively: every yes/no axis with these numbers (missing ones 0.5, undecided), a large, vague request.</summary>
    public static FakeBatchClassifier JevSaying(IReadOnlyDictionary<string, double> yesNo, bool largeAndVague = false) =>
        FakeBatchClassifier.Answering(Answers(yesNo, largeAndVague));

    private static Dictionary<string, BatchAnswer> Answers(IReadOnlyDictionary<string, double> yesNo, bool largeAndVague)
    {
        var answers = new Dictionary<string, BatchAnswer>(StringComparer.Ordinal);
        foreach (var (axis, noul) in yesNo)
        {
            answers[axis] = new NoulBatchAnswer(noul);
        }

        if (largeAndVague)
        {
            answers["complexity"] = new ScoreBatchAnswer(3, 0.9, new Dictionary<string, double>(StringComparer.Ordinal) { ["3"] = 0.9, ["2"] = 0.1 });
            answers["ambiguity"] = new ScoreBatchAnswer(2, 0.9, new Dictionary<string, double>(StringComparer.Ordinal) { ["2"] = 0.9, ["1"] = 0.1 });
        }

        return answers;
    }
}
