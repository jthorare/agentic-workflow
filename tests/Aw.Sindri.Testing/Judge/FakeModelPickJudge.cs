using Aw.Sindri.Core.Judge;

namespace Aw.Sindri.Testing.Judge;

/// <summary>The judge's model-pick question in memory: answers every call with the answer it was given and keeps the asks it saw.</summary>
public sealed class FakeModelPickJudge : IModelPickJudge
{
    private readonly List<ModelPickAsk> _asks = [];
    private ChooseAnswer _answer = new(ChooseStatus.Failed, Choice: null, "no answer given");

    /// <summary>The asks, in order.</summary>
    public IReadOnlyList<ModelPickAsk> Asks => _asks;

    /// <summary>Sets the answer of every later call.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The same instance.</returns>
    public FakeModelPickJudge Answers(ChooseAnswer answer)
    {
        _answer = answer;
        return this;
    }

    /// <summary>Answers every later call as Jev choosing <paramref name="key"/>.</summary>
    /// <param name="key">The candidate key ('m1', 'm2', ...).</param>
    /// <returns>The same instance.</returns>
    public FakeModelPickJudge Chooses(string key) => Answers(new ChooseAnswer(ChooseStatus.Decided, key, "model", Confidence: 0.9, Provider: "jev", DecisionRef: "decision-1"));

    /// <inheritdoc/>
    public ValueTask<ChooseAnswer> Pick(ModelPickAsk ask, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _asks.Add(ask);
        return ValueTask.FromResult(_answer);
    }
}
