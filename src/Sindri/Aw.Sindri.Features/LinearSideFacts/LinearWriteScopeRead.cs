using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Sindri.Features.LinearSideFacts;

/// <summary>Reads the scope a Linear write runs under: the owner's, once the current caller's own scope lets it in. The registration passes the Linear issues slice's check, so this slice holds no type of that one.</summary>
/// <param name="cancellationToken">Cancels the read.</param>
/// <returns>The owner's scope, or the caller's refusal, or the error when no profile yet.</returns>
internal delegate ValueTask<Result<ContextScope>> LinearWriteScopeRead(CancellationToken cancellationToken);
