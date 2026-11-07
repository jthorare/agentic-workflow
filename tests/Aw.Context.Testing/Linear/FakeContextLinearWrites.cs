using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Linear;

/// <summary>
/// A scripted <see cref="IContextLinearWrites"/> for the tests of whatever calls it. A test scripts the next write's outcome with <see cref="Next"/> or <see cref="NextError(ErrorDefinition, string)"/>, and a live read per key with <see cref="SetLive"/>;
/// every call that gets past the session and text-limit checks is recorded in <see cref="Calls"/> with its arguments, client ids included. An unscripted write answers Applied with a view built from the input (see the members).
/// </summary>
public sealed class FakeContextLinearWrites : IContextLinearWrites
{
    /// <summary>The team key of issues the fake creates.</summary>
    public string DefaultTeamKey { get; init; } = "ENG";

    /// <summary>Every call that was sent, in order: a call refused for a session or for text over a limit is not here.</summary>
    public IReadOnlyList<FakeLinearWriteCall> Calls { get; } = [];

    /// <summary>Scripts the next write (Create, Change, Comment, EditComment or Attach) to end this way. Scripts are used in order, one per write.</summary>
    /// <param name="outcome">How the write ends.</param>
    public void Next(LinearWriteOutcome outcome) => throw new NotSupportedException();

    /// <summary>Scripts the next write to be refused with this error.</summary>
    /// <param name="definition">The error's code.</param>
    /// <param name="message">What happened.</param>
    public void NextError(ErrorDefinition definition, string message) => throw new NotSupportedException();

    /// <summary>Scripts the next write to be refused with this error.</summary>
    /// <param name="error">The error.</param>
    public void NextError(Error error) => throw new NotSupportedException();

    /// <summary>Scripts what <see cref="Live"/> answers for the view's key. An unscripted key answers SND-LINEAR-005.</summary>
    /// <param name="view">The issue as Linear has it.</param>
    public void SetLive(LinearIssueView view) => throw new NotSupportedException();

    /// <summary>Scripts <see cref="Live"/> to fail for a key.</summary>
    /// <param name="key">The issue's key.</param>
    /// <param name="error">The error.</param>
    public void SetLiveError(LinearIssueKey key, Error error) => throw new NotSupportedException();

    /// <summary>Scripts <see cref="WriteKeyStatus"/>; unscripted it resolves.</summary>
    /// <param name="status">The status.</param>
    public void SetWriteKeyStatus(LinearWriteKeyStatus status) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueView>> Live(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteKeyStatus>> WriteKeyStatus(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> Create(LinearIssueDraft draft, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> Change(LinearIssueKey key, LinearIssueChange change, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> Comment(LinearIssueKey key, string body, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> EditComment(LinearIssueKey key, ExternalId commentId, string body, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> Attach(LinearIssueKey key, Uri url, string title, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
}
