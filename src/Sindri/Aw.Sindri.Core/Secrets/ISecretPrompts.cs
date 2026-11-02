namespace Aw.Sindri.Core.Secrets;

/// <summary>What `sindri index connect` asks the human at the terminal: a token, typed with echo off. Nothing that is read here is ever echoed, logged or stored by the prompt itself.</summary>
public interface ISecretPrompts
{
    /// <summary>Gets a value indicating whether a person can answer: standard input and output are a terminal.</summary>
    public bool Interactive { get; }

    /// <summary>Asks for a secret with echo off.</summary>
    /// <param name="question">The question.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer, untrimmed, or null when input ended.</returns>
    public ValueTask<string?> AskSecret(string question, CancellationToken cancellationToken);
}
