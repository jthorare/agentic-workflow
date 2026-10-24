using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>The code lens (S-CL, scoping station C3): the files, tests and data around some anchors. IContextEngine extends it; the workflow engine's 'input.context.code' block will call it in process (its descriptor is still pending).</summary>
public interface IContextCode
{
    /// <summary>The code lens around one or more anchors, read from one map snapshot with a lexical search of the repo at the map's commit.</summary>
    /// <param name="anchors">Node keys, aliases or labels; duplicates count once.</param>
    /// <param name="draft">True to read the latest draft; not together with <paramref name="mapVersion"/>.</param>
    /// <param name="mapVersion">A retained map version ('v2', 'v2@a41b07d'), or null for the current one.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The code lens; an anchor this caller can't read goes to 'missingAnchors'. SND-CONTEXT-003 when none resolves, 006 for a bad request, 037, 120, and 001/004/020/030/044. A lexical search that fails is a row of 'searched', never an error.</returns>
    public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken);
}
