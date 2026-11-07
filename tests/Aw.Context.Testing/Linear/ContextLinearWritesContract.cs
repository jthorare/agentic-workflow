using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Records;
using Aw.Kernel;
using Xunit;

namespace Aw.Context.Testing.Linear;

/// <summary>
/// The contract every <see cref="IContextLinearWrites"/> honours: a session is refused (SND-CONTEXT-020) on every method before anything is sent, a text over a limit is refused with SND-LINEAR-015 before anything is sent (and the limits themselves pass),
/// Create and Comment send the client id they were given, and an unknown outcome is a value, not an error. The implementation tells the contract how many requests went out, which client ids they carried and how to make the next write end unknown.
/// </summary>
/// <typeparam name="TWrites">The implementation.</typeparam>
public abstract class ContextLinearWritesContract<TWrites>
    where TWrites : IContextLinearWrites
{
    private static readonly string _owner = typeof(TWrites).Name;
    private static readonly LinearIssueKey _key = ContractCheck.Ok(_owner, LinearIssueKey.Parse("ENG-12"));
    private static readonly LinearClientId _clientId = ContractCheck.Ok(_owner, LinearClientId.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));
    private static readonly ExternalId _team = ExternalId.From("00000000-0000-0000-0000-00000000e001");
    private static readonly ExternalId _comment = ExternalId.From("00000000-0000-0000-0000-00000000c001");
    private static readonly Uri _url = new("https://github.com/acme/app/pull/7");
    private static readonly string _titleMax = new('t', 255);
    private static readonly string _textMax = new('x', 65_536);
    private static readonly string _textOver = new('x', 65_537);

    /// <summary>A fresh implementation that has sent nothing and whose writes succeed.</summary>
    /// <returns>The implementation.</returns>
    protected abstract TWrites Create();

    /// <summary>How many requests the implementation has sent to Linear (a read counts).</summary>
    /// <param name="writes">The implementation.</param>
    /// <returns>The count.</returns>
    protected abstract int Sent(TWrites writes);

    /// <summary>The client ids of the creates and comments that went out, in order.</summary>
    /// <param name="writes">The implementation.</param>
    /// <returns>The ids.</returns>
    protected abstract IReadOnlyList<LinearClientId> ClientIdsSent(TWrites writes);

    /// <summary>Makes the next write end unknown: sent, no answer, and the re-read didn't find it.</summary>
    /// <param name="writes">The implementation.</param>
    protected abstract void MakeNextWriteUnknown(TWrites writes);

    private static ContextScope Scope(ContextCallerKind kind) =>
        new(Settings: null, [], new ContextCaller(kind, kind == ContextCallerKind.Session ? ContextRepoName.From("split-panes") : null, ContextClient.Cli));

    private static LinearIssueDraft Draft(string title = "A title", string? description = null) => new(_team, title, description);

    private static LinearIssueChange ChangeTitle(string title) => new(Title: LinearFieldChange.Set(title));

    private static LinearIssueChange ChangeDescription(string? description) => new(Description: LinearFieldChange.Set<string?>(description));

    /// <summary>A session is refused on every method, and nothing is sent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_session_is_refused_on_every_method_and_nothing_is_sent()
    {
        var writes = Create();
        var session = Scope(ContextCallerKind.Session);
        var ct = CancellationToken.None;

        ContractCheck.Fails(_owner, await writes.Live(_key, session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.WriteKeyStatus(session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.Create(Draft(), _clientId, session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.Change(_key, ChangeTitle("New"), session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.Comment(_key, "hello", _clientId, session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.EditComment(_key, _comment, "hello", session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.Attach(_key, _url, "PR 7", session, ct), "SND-CONTEXT-020");
        ContractCheck.That(_owner, Sent(writes) == 0, "a refused session sent nothing");
    }

    /// <summary>A title or text over its limit is SND-LINEAR-015 on every method that takes one, and nothing is sent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Text_over_a_limit_is_refused_with_015_before_anything_is_sent()
    {
        var writes = Create();
        var human = Scope(ContextCallerKind.Human);
        var ct = CancellationToken.None;
        var refused = new (string What, Result<LinearWriteOutcome> Result)[]
        {
            ("create with an empty title", await writes.Create(Draft(title: ""), _clientId, human, ct)),
            ("create with a 256-character title", await writes.Create(Draft(title: new string('t', 256)), _clientId, human, ct)),
            ("create with a 65,537-character description", await writes.Create(Draft(description: _textOver), _clientId, human, ct)),
            ("change to an empty title", await writes.Change(_key, ChangeTitle(""), human, ct)),
            ("change to a 256-character title", await writes.Change(_key, ChangeTitle(new string('t', 256)), human, ct)),
            ("change to a 65,537-character description", await writes.Change(_key, ChangeDescription(_textOver), human, ct)),
            ("an empty comment", await writes.Comment(_key, "", _clientId, human, ct)),
            ("a 65,537-character comment", await writes.Comment(_key, _textOver, _clientId, human, ct)),
            ("an edit to an empty comment", await writes.EditComment(_key, _comment, "", human, ct)),
            ("an edit to a 65,537-character comment", await writes.EditComment(_key, _comment, _textOver, human, ct)),
        };

        foreach (var (what, result) in refused)
        {
            ContractCheck.Fails(_owner, result, "SND-LINEAR-015");
            ContractCheck.That(_owner, result.IsError && result.Error.Message.EndsWith("Nothing was changed.", StringComparison.Ordinal), $"{what}: the message says nothing was changed");
        }

        ContractCheck.That(_owner, Sent(writes) == 0, "text over a limit sent nothing");
    }

    /// <summary>The limits themselves are allowed: a 255-character title and 65,536 characters of text.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Text_at_a_limit_is_sent()
    {
        var writes = Create();
        var human = Scope(ContextCallerKind.Human);
        var ct = CancellationToken.None;

        _ = ContractCheck.Ok(_owner, await writes.Create(Draft(_titleMax, _textMax), _clientId, human, ct));
        _ = ContractCheck.Ok(_owner, await writes.Change(_key, new LinearIssueChange(Title: LinearFieldChange.Set(_titleMax), Description: LinearFieldChange.Set<string?>(_textMax)), human, ct));
        _ = ContractCheck.Ok(_owner, await writes.Comment(_key, _textMax, _clientId, human, ct));
        _ = ContractCheck.Ok(_owner, await writes.EditComment(_key, _comment, _textMax, human, ct));
    }

    /// <summary>Create and Comment send the client id they were given.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Create_and_comment_send_the_client_id_they_were_given()
    {
        var writes = Create();
        var human = Scope(ContextCallerKind.Human);
        var other = ContractCheck.Ok(_owner, LinearClientId.Parse("6ba7b810-9dad-41d1-80b4-00c04fd430c8"));

        _ = ContractCheck.Ok(_owner, await writes.Create(Draft(), _clientId, human, CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await writes.Comment(_key, "hello", other, human, CancellationToken.None));

        ContractCheck.That(_owner, ClientIdsSent(writes).SequenceEqual([_clientId, other]), "the create and the comment each sent their own client id, in order");
    }

    /// <summary>An unknown outcome is a success carrying <see cref="LinearWriteUnknown"/>, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Unknown_is_a_value_not_an_error()
    {
        var writes = Create();
        MakeNextWriteUnknown(writes);

        var result = await writes.Comment(_key, "hello", _clientId, Scope(ContextCallerKind.Human), CancellationToken.None);

        ContractCheck.That(_owner, ContractCheck.Ok(_owner, result) is LinearWriteUnknown, "the outcome is Unknown, as a success");
    }

    /// <summary>An attach by a human is sent; the call needs no client id.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_human_attach_is_sent()
    {
        var writes = Create();

        _ = ContractCheck.Ok(_owner, await writes.Attach(_key, _url, "PR 7", Scope(ContextCallerKind.Human), CancellationToken.None));

        ContractCheck.That(_owner, Sent(writes) == 1, "one request went out");
    }
}
