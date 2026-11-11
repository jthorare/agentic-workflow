namespace Aw.Sindri.Core.Text;

/// <summary>Finds the secrets in a text and says which rule matched which value (an adapter over Aw.Redaction). The staged scan uses it to tell a credential from a pointer.</summary>
public interface ISecretFinder
{
    /// <summary>The secrets in the text, in order. A scan that could not finish (a timeout) returns <paramref name="complete"/> = false.</summary>
    /// <param name="text">The text.</param>
    /// <param name="complete">False when the scan was blocked and the hits can't be trusted.</param>
    /// <returns>The hits.</returns>
    public IReadOnlyList<SecretHit> Find(string text, out bool complete);
}
