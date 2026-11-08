using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;

namespace Aw.Sindri.Host.CodeIndexHost;

/// <summary>The scope the code index is read in for the call running now: the profile's repos and the current caller (code-index spec §7, §10).</summary>
internal interface ICodeIndexScopeSource
{
    /// <summary>The scope of the call running now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or the error that says why there is none (SND-PROFILE-001 when the profile file is missing or unreadable).</returns>
    public ValueTask<Result<CodeIndexScope>> Current(CancellationToken cancellationToken);
}
