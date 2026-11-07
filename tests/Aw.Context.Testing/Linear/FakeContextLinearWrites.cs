using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Linear;

/// <summary>
/// A scripted <see cref="IContextLinearWrites"/> for the tests of whatever calls it. A test scripts the next write's outcome with <see cref="Next"/> or <see cref="NextError(ErrorDefinition, string)"/>, and a live read per key with <see cref="SetLive"/>;
/// every call that gets past the session and text-limit checks is recorded in <see cref="Calls"/> with its arguments, client ids included.
/// An unscripted write answers Applied with a view built from the input: Create makes 'ENG-1', 'ENG-2'... (see <see cref="DefaultTeamKey"/>) with the draft's fields and an id derived from the client id; Change applies the sent fields to the key's live view
/// (or a plain one); Comment, EditComment and Attach answer the key's live view unchanged, and EditComment of a comment <see cref="SetComment"/> put on another issue answers SND-LINEAR-005. A view a Create or Change made becomes that key's live view. An unscripted <see cref="Live"/> answers SND-LINEAR-005.
/// </summary>
public sealed class FakeContextLinearWrites : IContextLinearWrites
{
    private static readonly DateTimeOffset _updatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Lock _gate = new();
    private readonly List<FakeLinearWriteCall> _calls = [];
    private readonly Queue<Result<LinearWriteOutcome>> _scripts = new();
    private readonly Dictionary<LinearIssueKey, Result<LinearIssueView>> _live = [];
    private readonly Dictionary<ExternalId, LinearIssueKey> _comments = [];
    private LinearWriteKeyStatus _keyStatus = new LinearWriteKeyResolves();
    private int _created;

    /// <summary>The team key of issues the fake creates.</summary>
    public string DefaultTeamKey { get; init; } = "ENG";

    /// <summary>Every call that was sent, in order: a call refused for a session or for text over a limit is not here.</summary>
    public IReadOnlyList<FakeLinearWriteCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Says which issue a comment is on. EditComment of a comment set here on another issue answers SND-LINEAR-005 and sends nothing; a comment never set is taken to be on the issue the call names.</summary>
    /// <param name="commentId">The comment's id.</param>
    /// <param name="issue">The issue it is on.</param>
    public void SetComment(ExternalId commentId, LinearIssueKey issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        lock (_gate)
        {
            _comments[commentId] = issue;
        }
    }

    /// <summary>Scripts the next write (Create, Change, Comment, EditComment or Attach) to end this way. Scripts are used in order, one per write. To answer an applied write that only the re-read found, script <see cref="NextFoundOnReread"/>.</summary>
    /// <param name="outcome">How the write ends.</param>
    public void Next(LinearWriteOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        lock (_gate)
        {
            _scripts.Enqueue(outcome);
        }
    }

    /// <summary>Scripts the next write to end Applied with this view and <see cref="LinearWriteApplied.FoundOnReread"/> true: Linear gave no answer and the re-read found the write.</summary>
    /// <param name="stored">The issue as the re-read found and stored it.</param>
    public void NextFoundOnReread(LinearIssueView stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        Next(new LinearWriteApplied(stored) { FoundOnReread = true });
    }

    /// <summary>Scripts the next write to be refused with this error.</summary>
    /// <param name="definition">The error's code.</param>
    /// <param name="message">What happened.</param>
    public void NextError(ErrorDefinition definition, string message)
    {
        ArgumentNullException.ThrowIfNull(definition);
        NextError(definition.With(message));
    }

    /// <summary>Scripts the next write to be refused with this error.</summary>
    /// <param name="error">The error.</param>
    public void NextError(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        lock (_gate)
        {
            _scripts.Enqueue(error);
        }
    }

    /// <summary>Scripts what <see cref="Live"/> answers for the view's key. An unscripted key answers SND-LINEAR-005.</summary>
    /// <param name="view">The issue as Linear has it.</param>
    public void SetLive(LinearIssueView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        lock (_gate)
        {
            _live[view.Key] = view;
        }
    }

    /// <summary>Scripts <see cref="Live"/> to fail for a key.</summary>
    /// <param name="key">The issue's key.</param>
    /// <param name="error">The error.</param>
    public void SetLiveError(LinearIssueKey key, Error error)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(error);
        lock (_gate)
        {
            _live[key] = error;
        }
    }

    /// <summary>Scripts <see cref="WriteKeyStatus"/>; unscripted it resolves.</summary>
    /// <param name="status">The status.</param>
    public void SetWriteKeyStatus(LinearWriteKeyStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        lock (_gate)
        {
            _keyStatus = status;
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueView>> Live(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken)
    {
        if (Refuse(scope) is { } refused)
        {
            return ValueTask.FromResult<Result<LinearIssueView>>(refused);
        }

        lock (_gate)
        {
            _calls.Add(new FakeLinearWriteCall("Live", key));
            return ValueTask.FromResult(_live.TryGetValue(key, out var answer) ? answer : LinearHubErrors.IssueNotFound.With($"No issue {key} in your teams"));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteKeyStatus>> WriteKeyStatus(ContextScope scope, CancellationToken cancellationToken)
    {
        if (Refuse(scope) is { } refused)
        {
            return ValueTask.FromResult<Result<LinearWriteKeyStatus>>(refused);
        }

        lock (_gate)
        {
            return ValueTask.FromResult<Result<LinearWriteKeyStatus>>(_keyStatus);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> Create(LinearIssueDraft draft, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(clientId);
        return Write(scope, LinearWriteLimits.Title(draft.Title) ?? LinearWriteLimits.Text("description", draft.Description, required: false), new FakeLinearWriteCall("Create", ClientId: clientId, Draft: draft), () =>
        {
            var key = LinearIssueKey.Parse(string.Create(CultureInfo.InvariantCulture, $"{DefaultTeamKey}-{++_created}")).Value;
            var created = new LinearIssueView(
                ExternalId.From("fake-issue-" + clientId.Value), key, DefaultTeamKey, draft.Title, draft.Description, draft.StateId ?? TodoId, draft.StateId is null ? "Todo" : "State", draft.AssigneeId, draft.Priority ?? 0,
                draft.Estimate, draft.CycleId, draft.ProjectId, draft.ParentId, draft.LabelIds ?? [], _updatedAt);
            return Store(created);
        });
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> Change(LinearIssueKey key, LinearIssueChange change, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(change);
        return Write(scope, LinearWriteLimits.Change(change), new FakeLinearWriteCall("Change", key, Change: change), () => Store(Apply(ViewOf(key), change)));
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> Comment(LinearIssueKey key, string body, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(clientId);
        return Write(scope, LinearWriteLimits.Text("comment", body, required: true), new FakeLinearWriteCall("Comment", key, clientId, Body: body), () => ViewOf(key));
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> EditComment(LinearIssueKey key, ExternalId commentId, string body, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            if (Refuse(scope) is null && LinearWriteLimits.Text("comment", body, required: true) is null && _comments.TryGetValue(commentId, out var on) && on != key)
            {
                return ValueTask.FromResult<Result<LinearWriteOutcome>>(LinearHubErrors.IssueNotFound.With($"No comment {commentId.Value} on {key}, so nothing was sent."));
            }
        }

        return Write(scope, LinearWriteLimits.Text("comment", body, required: true), new FakeLinearWriteCall("EditComment", key, Body: body, CommentId: commentId), () => ViewOf(key));
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearWriteOutcome>> Attach(LinearIssueKey key, Uri url, string title, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(url);
        return Write(scope, LinearWriteLimits.Attachment(url, title), new FakeLinearWriteCall("Attach", key, Url: url, Title: title), () => ViewOf(key));
    }

    private static ExternalId TodoId => ExternalId.From("fake-state-todo");

    private static Error? Refuse(ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Caller.Kind == ContextCallerKind.Session ? ContextErrors.SessionsDisabled.With("Linear writes are the owner's, not an agent session's") : null;
    }

    // why: the session and limit checks come before the record and the script, so a refused call sends nothing and uses up no script
    private ValueTask<Result<LinearWriteOutcome>> Write(ContextScope scope, Error? limit, FakeLinearWriteCall call, Func<LinearIssueView> unscripted)
    {
        if ((Refuse(scope) ?? limit) is { } refused)
        {
            return ValueTask.FromResult<Result<LinearWriteOutcome>>(refused);
        }

        lock (_gate)
        {
            _calls.Add(call);
            return ValueTask.FromResult(_scripts.Count > 0 ? _scripts.Dequeue() : new LinearWriteApplied(unscripted()));
        }
    }

    private LinearIssueView Store(LinearIssueView view)
    {
        _live[view.Key] = view;
        return view;
    }

    private LinearIssueView ViewOf(LinearIssueKey key) =>
        _live.TryGetValue(key, out var answer) && answer.IsSuccess
            ? answer.Value
            : new LinearIssueView(ExternalId.From("fake-issue-" + key), key, key.Team, "Issue " + key, Description: null, TodoId, "Todo", AssigneeId: null, Priority: 0, Estimate: null, CycleId: null, ProjectId: null, ParentId: null, [], _updatedAt);

    private static LinearIssueView Apply(LinearIssueView view, LinearIssueChange change) => view with
    {
        Title = Pick(change.Title, view.Title),
        Description = Pick(change.Description, view.Description),
        StateId = Pick(change.StateId, view.StateId),
        AssigneeId = Pick(change.AssigneeId, view.AssigneeId),
        Priority = Pick(change.Priority, view.Priority),
        Estimate = Pick(change.Estimate, view.Estimate),
        CycleId = Pick(change.CycleId, view.CycleId),
        ProjectId = Pick(change.ProjectId, view.ProjectId),
        ParentId = Pick(change.ParentId, view.ParentId),
        LabelIds = Pick(change.LabelIds, view.LabelIds),
    };

    private static T Pick<T>(LinearFieldChange<T> field, T current) => field.IsSet ? field.Value! : current;
}
