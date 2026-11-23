using Aw.Context.Core;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Health;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The one rule for whether a context source needs a look (issue #3430): the server decides it, and the app reads the flag, not the pull words.</summary>
internal static class IndexSourceLook
{
    /// <summary>Whether this source needs a look: its pull failed or backs off; it is idle, holds nothing and never committed although a pass has ended
    /// (before the first pass ends, a fresh source is not a look, #3183); or, with no pull read, it is unusable with a code. A running pull, or a pull
    /// word this rule doesn't name, is normal work until it is added here.</summary>
    /// <param name="p">The source's progress.</param>
    /// <returns>True when the source needs a look.</returns>
    internal static bool For(SourceProgress p) => p.Pull switch
    {
        null => !p.Usable && !string.IsNullOrEmpty(p.LastCode),
        "failed" or "backoff" => true,
        "idle" => p.Records == 0 && p.LastCommit is null && p.LastSync is not null,
        _ => false,
    };

    /// <summary>The source's health problem (#4010): one line with its code, or null when the source needs no look.</summary>
    /// <param name="p">The source's progress.</param>
    /// <returns>The problem, or null.</returns>
    internal static HealthProblem? Problem(SourceProgress p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (!For(p))
        {
            return null;
        }

        var name = p.Source.Value;
        var code = p.LastError ?? p.LastCode ?? ContextErrors.SourceUnavailable.Code;   // a pass that found nothing carries no code of its own
        var failed = p.LastErrorText ?? "its last pull failed";
        var words = p.Pull switch
        {
            "backoff" => failed + ", retrying",
            "failed" => failed,
            "idle" => "a pass ended and found nothing",
            _ => "it can't be used yet",
        };
        return new HealthProblem(code, $"{name}: {words}", $"the {name} source needs a look");
    }
}
