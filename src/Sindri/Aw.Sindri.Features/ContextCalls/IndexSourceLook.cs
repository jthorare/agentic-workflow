using Aw.Context.Core.Sync;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The one rule for whether a context source needs a look (issue #3430): the server decides it, and the app reads the flag, not the pull words.</summary>
internal static class IndexSourceLook
{
    /// <summary>Whether this source needs a look.</summary>
    /// <param name="p">The source's progress.</param>
    /// <returns>True when the source needs a look.</returns>
    internal static bool For(SourceProgress p) => p.Pull switch
    {
        null => !p.Usable && !string.IsNullOrEmpty(p.LastCode),
        "failed" or "backoff" => true,
        "idle" => p.Records == 0 && p.LastCommit is null,
        _ => false,
    };
}
