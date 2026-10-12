using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>
/// A budget for one whole scrub. The regex match timeout restarts on every match, so it doesn't bound a scan with many
/// matches; the built-in rules stay linear by review, but a configured pattern may be slow on every match without ever
/// timing out. A redactor with extra patterns checks this deadline around the placeholder scan, before every rule's scan and after every match, and blocks when it passes.
/// </summary>
/// <param name="Clock">The clock the caller injected.</param>
/// <param name="Start">The clock's timestamp when the scrub started.</param>
/// <param name="Budget">The time the whole scrub may take.</param>
internal readonly record struct ScanDeadline(TimeProvider Clock, long Start, TimeSpan Budget)
{
    /// <summary>Throws when the scrub has run past its budget, so the scrub blocks like a match timeout.</summary>
    /// <exception cref="RegexMatchTimeoutException">The budget is spent.</exception>
    public void Check()
    {
        if (Clock.GetElapsedTime(Start) > Budget)
        {
            throw new RegexMatchTimeoutException("The scrub ran past its whole-scan budget.");
        }
    }
}
