using Aw.Kernel;
using Aw.Kernel.Testing;

namespace Aw.Forge.Testing;

/// <summary>The contract bases' assertions: a broken expectation is a <see cref="ContractViolationException"/>.</summary>
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

    /// <summary>Returns the value of a result that must be a success.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="what">What was being done.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ContractViolationException">The result is an error.</exception>
    public static T Ok<T>(Result<T> result, string what) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"contract: {what} failed with {result.Error.Code}: {result.Error.Message}");

    /// <summary>Requires a result to be the error with <paramref name="code"/>.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="code">The expected AW-FORGE code.</param>
    /// <param name="what">What was being done.</param>
    /// <returns>The error's message.</returns>
    /// <exception cref="ContractViolationException">The result isn't that error.</exception>
    public static string Fails<T>(Result<T> result, string code, string what)
    {
        if (result.IsSuccess)
        {
            throw new ContractViolationException($"contract: {what} succeeded; expected {code}");
        }

        return result.Error.Code == code
            ? result.Error.Message
            : throw new ContractViolationException($"contract: {what} failed with {result.Error.Code}; expected {code}");
    }
}
