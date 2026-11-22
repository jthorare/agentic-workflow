using Aw.Kernel.Testing;

namespace Aw.Transcripts.Testing;

/// <summary>The contract base's assertions: a broken expectation is a <see cref="ContractViolationException"/> (no xunit.v3.assert here, like Aw.Kernel.Testing).</summary>
internal static class Expect
{
    /// <summary>Throws when <paramref name="condition"/> is false.</summary>
    public static void That(bool condition, string what)
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: {what}");
        }
    }

    /// <summary>Throws when the two sequences differ, showing both.</summary>
    public static void Same<T>(IEnumerable<T> expected, IEnumerable<T> actual, string what)
    {
        var want = expected.ToList();
        var got = actual.ToList();
        if (!want.SequenceEqual(got))
        {
            throw new ContractViolationException($"contract: {what}\n  expected: {string.Join(" | ", want)}\n  actual:   {string.Join(" | ", got)}");
        }
    }
}
