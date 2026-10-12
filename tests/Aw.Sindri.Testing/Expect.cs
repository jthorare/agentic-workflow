using System.Runtime.CompilerServices;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Testing;

/// <summary>The checks the contract bases make; a failure is a <see cref="ContractViolationException"/> naming the expression.</summary>
public static class Expect
{
    /// <summary>Fails unless <paramref name="condition"/> holds.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="expression">The condition's source text.</param>
    public static void True(bool condition, [CallerArgumentExpression(nameof(condition))] string expression = "")
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: expected {expression}");
        }
    }

    /// <summary>Fails unless the values are equal.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="expression">The actual value's source text.</param>
    /// <typeparam name="T">The value type.</typeparam>
    public static void Equal<T>(T expected, T actual, [CallerArgumentExpression(nameof(actual))] string expression = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new ContractViolationException($"contract: {expression} was {actual}, expected {expected}");
        }
    }

    /// <summary>Fails unless the sequences are equal, item by item.</summary>
    /// <param name="expected">The expected items.</param>
    /// <param name="actual">The actual items.</param>
    /// <param name="expression">The actual value's source text.</param>
    /// <typeparam name="T">The item type.</typeparam>
    public static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, [CallerArgumentExpression(nameof(actual))] string expression = "")
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new ContractViolationException($"contract: {expression} was [{string.Join(", ", actual)}], expected [{string.Join(", ", expected)}]");
        }
    }

    /// <summary>The only item, or a failure.</summary>
    /// <param name="items">The items.</param>
    /// <param name="expression">Their source text.</param>
    /// <typeparam name="T">The item type.</typeparam>
    /// <returns>The single item.</returns>
    public static T Only<T>(IReadOnlyList<T> items, [CallerArgumentExpression(nameof(items))] string expression = "") =>
        items.Count == 1 ? items[0] : throw new ContractViolationException($"contract: {expression} had {items.Count} items, expected 1");
}
