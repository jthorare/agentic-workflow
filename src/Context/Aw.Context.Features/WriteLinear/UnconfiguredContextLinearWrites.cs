using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.WriteLinear;

/// <summary>The Linear writes of a host that wired none: a session is refused first (SND-CONTEXT-020, as the real writes do), then every call says the store can't answer (SND-CONTEXT-004) and sends nothing. A host that adds Aw.Context.Infrastructure's registration gets the real ones.</summary>
internal sealed class UnconfiguredContextLinearWrites : IContextLinearWrites
{
    public ValueTask<Result<LinearIssueView>> Live(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearIssueView>(scope));

    public ValueTask<Result<LinearWriteKeyStatus>> WriteKeyStatus(ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteKeyStatus>(scope));

    public ValueTask<Result<LinearWriteOutcome>> Create(LinearIssueDraft draft, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>(scope));

    public ValueTask<Result<LinearWriteOutcome>> Change(LinearIssueKey key, LinearIssueChange change, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>(scope));

    public ValueTask<Result<LinearWriteOutcome>> Comment(LinearIssueKey key, string body, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>(scope));

    public ValueTask<Result<LinearWriteOutcome>> EditComment(LinearIssueKey key, ExternalId commentId, string body, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>(scope));

    public ValueTask<Result<LinearWriteOutcome>> Attach(LinearIssueKey key, Uri url, string title, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>(scope));

    private static Result<T> Unwired<T>(ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Caller.Kind == ContextCallerKind.Session ? ContextErrors.SessionsDisabled.With("Linear writes are the owner's, not an agent session's") : Unavailable<T>();
    }

    private static Result<T> Unavailable<T>() =>
        ContextErrors.StoreUnavailable.With("no Linear write client is wired in this host (fix: add Aw.Context.Infrastructure's registration)");
}
