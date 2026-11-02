using Aw.Kernel.Testing;

namespace Aw.Embeddings.Testing;

/// <summary>The contract bases' one assertion: a broken expectation is a <see cref="ContractViolationException"/>.</summary>
internal static class Expect
{
    /// <summary>Throws when <paramref name="condition"/> is false.</summary>
    /// <param name="condition">The expectation.</param>
    /// <param name="what">What the contract promises.</param>
    /// <exception cref="ContractViolationException">The expectation is false.</exception>
    public static void That(bool condition, string what)
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: {what}");
        }
    }
}
