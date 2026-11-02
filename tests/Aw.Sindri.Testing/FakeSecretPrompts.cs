using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Testing;

/// <summary>Secret prompts answered from a script; remembers what it was asked (never the answers).</summary>
public sealed class FakeSecretPrompts : ISecretPrompts
{
    private readonly Queue<string?> _answers = new();

    /// <summary>Gets or sets a value indicating whether a person is there to answer.</summary>
    public bool Interactive { get; set; } = true;

    /// <summary>Gets the questions asked, in order.</summary>
    public IList<string> Asked { get; } = [];

    /// <summary>Gets the number of times a secret was asked for.</summary>
    public int SecretsAsked { get; private set; }

    /// <summary>Queues the next answers, in the order they are asked.</summary>
    /// <param name="answers">The answers; null ends input.</param>
    public void Answer(params string?[] answers)
    {
        foreach (var answer in answers)
        {
            _answers.Enqueue(answer);
        }
    }

    /// <inheritdoc />
    public ValueTask<string?> AskSecret(string question, CancellationToken cancellationToken)
    {
        Asked.Add(question);
        SecretsAsked++;
        return ValueTask.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
    }
}
