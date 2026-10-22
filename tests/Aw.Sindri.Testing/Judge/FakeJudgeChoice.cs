using Aw.Sindri.Core.Judge;

namespace Aw.Sindri.Testing.Judge;

/// <summary>The judge's choose question in memory: answers every call with the answer it was given and keeps the requests it saw.</summary>
public sealed class FakeJudgeChoice : IJudgeChoice
{
    private readonly List<ChooseRequest> _requests = [];
    private ChooseAnswer _answer = new(ChooseStatus.Failed, Choice: null, "no answer given");

    /// <summary>The requests asked, in order.</summary>
    public IReadOnlyList<ChooseRequest> Requests => _requests;

    /// <summary>Sets the answer of every later call.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The same instance.</returns>
    public FakeJudgeChoice Answers(ChooseAnswer answer)
    {
        _answer = answer;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<ChooseAnswer> Choose(ChooseRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Add(request);
        return ValueTask.FromResult(_answer);
    }
}
