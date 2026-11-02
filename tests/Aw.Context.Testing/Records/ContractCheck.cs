using Aw.Kernel;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Records;

/// <summary>The checks the record-store contract bases share: a failed one throws <see cref="ContractViolationException"/> naming the implementation.</summary>
internal static class ContractCheck
{
    /// <summary>Fails the contract unless <paramref name="holds"/>.</summary>
    /// <param name="owner">The implementation's type name.</param>
    /// <param name="holds">The condition.</param>
    /// <param name="what">What should hold.</param>
    public static void That(string owner, bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{owner}: {what}");
        }
    }

    /// <summary>Unwraps a result that must be a success.</summary>
    /// <typeparam name="T">The value.</typeparam>
    /// <param name="owner">The implementation's type name.</param>
    /// <param name="result">The result.</param>
    /// <returns>The value.</returns>
    public static T Ok<T>(string owner, Result<T> result) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"{owner}: {result.Error.Code} {result.Error.Message}");

    /// <summary>Fails the contract unless the result is an error with the given code.</summary>
    /// <typeparam name="T">The value.</typeparam>
    /// <param name="owner">The implementation's type name.</param>
    /// <param name="result">The result.</param>
    /// <param name="code">The expected error code.</param>
    public static void Fails<T>(string owner, Result<T> result, string code) =>
        That(owner, result.IsError && result.Error.Code == code, $"it answers {code} (got {(result.IsError ? result.Error.Code : "a success")})");
}
