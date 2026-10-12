using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Testing;

/// <summary>
/// The IModelProvider fake: answers from a script (a result or a throw per call; the last step repeats). Passes
/// ModelProviderContract: a scripted decision outside the question's outputs comes back errored 'out-of-enum'. A
/// scripted throw breaks the contract on purpose, for FailOpenModelProvider's tests.
/// </summary>
/// <param name="name">Which provider it plays.</param>
/// <param name="classes">The classes it covers (message-meta when none are given).</param>
public sealed class ScriptedProvider(ProviderName name, params ContentClass[] classes) : IModelProvider
{
    private readonly Queue<Func<ProviderResult>> _script = new();
    private readonly List<ProviderCall> _calls = [];

    /// <inheritdoc />
    public ProviderName Name => name;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes { get; } = classes.Length == 0 ? new HashSet<ContentClass> { ContentClass.MessageMeta } : [.. classes];

    /// <summary>Every call so far.</summary>
    public IReadOnlyList<ProviderCall> Calls => _calls;

    /// <summary>Queues an answer.</summary>
    /// <param name="result">The answer.</param>
    /// <returns>This provider.</returns>
    public ScriptedProvider Returns(ProviderResult result)
    {
        _script.Enqueue(() => result);
        return this;
    }

    /// <summary>Queues a decided answer with reason 'model'.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="confidence">The confidence.</param>
    /// <param name="probabilities">The probabilities, if any.</param>
    /// <returns>This provider.</returns>
    public ScriptedProvider Decides(string decision, double confidence, IReadOnlyDictionary<string, double>? probabilities = null) =>
        Returns(new ProviderDecided(decision, confidence, "model") { Probabilities = probabilities });

    /// <summary>Queues a throw.</summary>
    /// <returns>This provider.</returns>
    public ScriptedProvider Throws()
    {
        _script.Enqueue(static () => throw new InvalidOperationException("scripted provider failure"));
        return this;
    }

    /// <inheritdoc />
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add(new ProviderCall(question, input, budget));
        var step = _script.Count switch
        {
            0 => throw new InvalidOperationException($"{ProviderNames.Name(name)} has no scripted answer"),
            1 => _script.Peek(),
            _ => _script.Dequeue(),
        };
        var result = step();
        // Like every adapter (ModelProviderContract), a decision outside the outputs is errored, never decided.
        return ValueTask.FromResult(result is ProviderDecided decided && !question.Outputs.Contains(decided.Decision, StringComparer.Ordinal)
            ? new ProviderErrored("out-of-enum")
            : result);
    }
}
