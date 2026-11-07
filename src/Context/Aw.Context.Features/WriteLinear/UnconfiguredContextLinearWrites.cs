using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.WriteLinear;

/// <summary>The Linear writes of a host that wired none: every call says the store can't answer (SND-CONTEXT-004) and sends nothing. The slice that adds the write client replaces it.</summary>
internal sealed class UnconfiguredContextLinearWrites : IContextLinearWrites
{
    public ValueTask<Result<LinearIssueView>> Live(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearIssueView>());

    public ValueTask<Result<LinearWriteKeyStatus>> WriteKeyStatus(ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteKeyStatus>());

    public ValueTask<Result<LinearWriteOutcome>> Create(LinearIssueDraft draft, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>());

    public ValueTask<Result<LinearWriteOutcome>> Change(LinearIssueKey key, LinearIssueChange change, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>());

    public ValueTask<Result<LinearWriteOutcome>> Comment(LinearIssueKey key, string body, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>());

    public ValueTask<Result<LinearWriteOutcome>> EditComment(LinearIssueKey key, ExternalId commentId, string body, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>());

    public ValueTask<Result<LinearWriteOutcome>> Attach(LinearIssueKey key, Uri url, string title, ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearWriteOutcome>());

    private static Result<T> Unwired<T>() =>
        ContextErrors.StoreUnavailable.With("no Linear write client is wired in this host (fix: add Aw.Context.Infrastructure's registration)");
}
