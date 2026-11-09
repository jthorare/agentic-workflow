using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The scope Linear writes run under: the owner's (the write key is theirs), over the profile, once the current caller's own reads would let it in (a session: sessions on, a repo the profile maps).</summary>
internal interface ILinearWriteScope
{
    /// <summary>Reads the scope for a write.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope; the caller's refusal (SND-CONTEXT-020, 021, 003); or the error when no profile yet.</returns>
    public ValueTask<Result<ContextScope>> Write(CancellationToken cancellationToken);
}
