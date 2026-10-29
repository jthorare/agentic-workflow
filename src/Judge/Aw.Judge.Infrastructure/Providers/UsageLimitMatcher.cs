namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// Tells a harness's usage-limit message from any other failure text. Each pattern is anchored at the start of the message
/// (the real strings begin "You've hit your" or "Claude usage limit reached"), because an error can echo the prompt, which carries
/// repo paths and text. Callers hand it only text from a channel the CLI marks as an error, never a model's answer. No reset time
/// is parsed.
/// </summary>
#pragma warning disable MA0182 // the stub: the providers call it in the next commit
internal static class UsageLimitMatcher
{
    /// <summary>The id of the pattern a message matches.</summary>
    /// <param name="message">The error text.</param>
    /// <returns>The pattern's id, or null when the message is not a usage limit.</returns>
    public static string? Match(string? message) => throw new NotSupportedException();
}
