using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// Runs a named Messages red test (messages spec §14, M1s) so main stays green while its stub throws: a NotSupportedException from the stub skips the
/// test with the phase that builds it; any other exception or a failed assertion fails it as usual. The day a phase replaces its stub, the test runs for
/// real with no edit.
/// </summary>
internal static class RedUntil
{
    /// <summary>Runs <paramref name="body"/>; skips while the stub it reaches throws.</summary>
    /// <param name="phase">The phase that builds the stub ('M1b').</param>
    /// <param name="body">The test's body.</param>
    public static void Phase(string phase, Action body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            body();
        }
        catch (NotSupportedException e) when (IsStub(e))
        {
            Assert.Skip($"red until {phase}: the stub throws ({e.Message})");
        }
    }

    /// <summary>Runs <paramref name="body"/>; skips while the stub it reaches throws.</summary>
    /// <param name="phase">The phase that builds the stub ('M1c').</param>
    /// <param name="body">The test's body.</param>
    /// <returns>The run.</returns>
    public static async Task Phase(string phase, Func<Task> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            await body();
        }
        catch (NotSupportedException e) when (IsStub(e))
        {
            Assert.Skip($"red until {phase}: the stub throws ({e.Message})");
        }
    }

    // only a Messages stub's own refusal skips; any other NotSupportedException (System.Text.Json's missing metadata is one) fails the test
    private static bool IsStub(NotSupportedException e) => e.Message.Contains(MessageStubs.NotBuiltText, StringComparison.Ordinal);
}
