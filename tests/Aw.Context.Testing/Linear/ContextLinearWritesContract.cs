using System.Text.Json;
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
/// Create and Comment send the client id they were given, and an unknown outcome is a value, not an error. The implementation tells the contract how many requests went out (and how many of them were writes), which client ids they carried and how to make the next write end unknown;
/// an implementation that talks to a real (or fake) Linear overrides <see cref="HumanSettings"/> and the ids so the writes land on things that exist there.
/// </summary>
/// <typeparam name="TWrites">The implementation.</typeparam>
public abstract class ContextLinearWritesContract<TWrites>
    where TWrites : IContextLinearWrites
{
    private static readonly string _owner = typeof(TWrites).Name;
    private static readonly LinearClientId _clientId = ContractCheck.Ok(_owner, LinearClientId.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));
    private static readonly LinearClientId _otherClientId = ContractCheck.Ok(_owner, LinearClientId.Parse("6ba7b810-9dad-41d1-80b4-00c04fd430c8"));
    private static readonly Uri _url = new("https://github.com/acme/app/pull/7");
    private static readonly string _titleMax = new('t', 255);
    private static readonly string _textMax = new('x', 65_536);
    private static readonly string _textOver = new('x', 65_537);

    /// <summary>The profile settings of the human caller's scope. Null (the default) suits an implementation that never reads them; one that does returns a profile naming its Linear.</summary>
    protected virtual JsonElement? HumanSettings => null;

    /// <summary>The team the contract creates issues in.</summary>
    protected virtual ExternalId TeamId => ExternalId.From("00000000-0000-0000-0000-00000000e001");

    /// <summary>The key of an issue the contract changes, comments on and attaches to; it exists in the implementation's Linear.</summary>
    protected virtual LinearIssueKey IssueKey => ContractCheck.Ok(_owner, LinearIssueKey.Parse("ENG-12"));

    /// <summary>The id of a comment on <see cref="IssueKey"/> the contract edits.</summary>
    protected virtual ExternalId CommentId => ExternalId.From("00000000-0000-0000-0000-00000000c001");

    /// <summary>A fresh implementation that has sent nothing and whose writes succeed.</summary>
    /// <returns>The implementation.</returns>
    protected abstract TWrites Create();

    /// <summary>How many requests the implementation has sent to Linear (a read counts).</summary>
    /// <param name="writes">The implementation.</param>
    /// <returns>The count.</returns>
    protected abstract int Sent(TWrites writes);

    /// <summary>How many of the requests were writes (mutations): reads are not counted.</summary>
    /// <param name="writes">The implementation.</param>
    /// <returns>The count.</returns>
    protected abstract int WritesSent(TWrites writes);

    /// <summary>The client ids of the creates and comments that went out, in order.</summary>
    /// <param name="writes">The implementation.</param>
    /// <returns>The ids.</returns>
    protected abstract IReadOnlyList<LinearClientId> ClientIdsSent(TWrites writes);

    /// <summary>Makes the next write end unknown: sent, no answer, and the re-read didn't find it.</summary>
    /// <param name="writes">The implementation.</param>
    protected abstract void MakeNextWriteUnknown(TWrites writes);

    /// <summary>The id of a comment that exists, but on another issue than <see cref="IssueKey"/>.</summary>
    /// <param name="writes">The implementation.</param>
    /// <returns>The comment's id.</returns>
    protected abstract ExternalId CommentOnAnotherIssue(TWrites writes);

    // why: a session's scope carries no settings, so a session refused first proves the check comes before the settings are read
    private ContextScope Scope(ContextCallerKind kind) =>
        new(kind == ContextCallerKind.Human ? HumanSettings : null, [], new ContextCaller(kind, kind == ContextCallerKind.Session ? ContextRepoName.From("split-panes") : null, ContextClient.Cli));

    private LinearIssueDraft Draft(string title = "A title", string? description = null) => new(TeamId, title, description);

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

        ContractCheck.Fails(_owner, await writes.Live(IssueKey, session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.WriteKeyStatus(session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.Create(Draft(), _clientId, session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.Change(IssueKey, ChangeTitle("New"), session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.Comment(IssueKey, "hello", _clientId, session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.EditComment(IssueKey, CommentId, "hello", session, ct), "SND-CONTEXT-020");
        ContractCheck.Fails(_owner, await writes.Attach(IssueKey, _url, "PR 7", session, ct), "SND-CONTEXT-020");
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
            ("create with a whitespace title", await writes.Create(Draft(title: "   "), _clientId, human, ct)),
            ("change to a whitespace title", await writes.Change(IssueKey, ChangeTitle("  "), human, ct)),
            ("create with a 256-character title", await writes.Create(Draft(title: new string('t', 256)), _clientId, human, ct)),
            ("create with a 65,537-character description", await writes.Create(Draft(description: _textOver), _clientId, human, ct)),
            ("change to an empty title", await writes.Change(IssueKey, ChangeTitle(""), human, ct)),
            ("change to a 256-character title", await writes.Change(IssueKey, ChangeTitle(new string('t', 256)), human, ct)),
            ("change to a 65,537-character description", await writes.Change(IssueKey, ChangeDescription(_textOver), human, ct)),
            ("an empty comment", await writes.Comment(IssueKey, "", _clientId, human, ct)),
            ("a 65,537-character comment", await writes.Comment(IssueKey, _textOver, _clientId, human, ct)),
            ("an edit to an empty comment", await writes.EditComment(IssueKey, CommentId, "", human, ct)),
            ("an edit to a 65,537-character comment", await writes.EditComment(IssueKey, CommentId, _textOver, human, ct)),
            ("an attach with a relative link", await writes.Attach(IssueKey, new Uri("/acme/app/pull/7", UriKind.Relative), "PR 7", human, ct)),
            ("an attach with an http link", await writes.Attach(IssueKey, new Uri("http://github.com/acme/app/pull/7"), "PR 7", human, ct)),
            ("an attach with an ftp link", await writes.Attach(IssueKey, new Uri("ftp://example.com/pull/7"), "PR 7", human, ct)),
            ("an attach with a sindri link to another host", await writes.Attach(IssueKey, new Uri("sindri://other/itm_1"), "Item", human, ct)),
            ("an attach with an empty title", await writes.Attach(IssueKey, _url, "", human, ct)),
            ("an attach with a 256-character title", await writes.Attach(IssueKey, _url, new string('t', 256), human, ct)),
        };

        foreach (var (what, result) in refused)
        {
            ContractCheck.Fails(_owner, result, "SND-LINEAR-015");
            ContractCheck.That(_owner, result.IsError && result.Error.Message.StartsWith("Not sent: ", StringComparison.Ordinal) && result.Error.Message.EndsWith("Nothing was changed.", StringComparison.Ordinal), $"{what}: the message says it was not sent and nothing was changed");
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
        _ = ContractCheck.Ok(_owner, await writes.Change(IssueKey, new LinearIssueChange(Title: LinearFieldChange.Set(_titleMax), Description: LinearFieldChange.Set<string?>(_textMax)), human, ct));
        _ = ContractCheck.Ok(_owner, await writes.Comment(IssueKey, _textMax, _otherClientId, human, ct));
        _ = ContractCheck.Ok(_owner, await writes.EditComment(IssueKey, CommentId, _textMax, human, ct));
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
        _ = ContractCheck.Ok(_owner, await writes.Comment(IssueKey, "hello", other, human, CancellationToken.None));

        ContractCheck.That(_owner, ClientIdsSent(writes).SequenceEqual([_clientId, other]), "the create and the comment each sent their own client id, in order");
    }

    /// <summary>An unknown outcome is a success carrying <see cref="LinearWriteUnknown"/>, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Unknown_is_a_value_not_an_error()
    {
        var writes = Create();
        MakeNextWriteUnknown(writes);

        var result = await writes.Comment(IssueKey, "hello", _clientId, Scope(ContextCallerKind.Human), CancellationToken.None);

        ContractCheck.That(_owner, ContractCheck.Ok(_owner, result) is LinearWriteUnknown, "the outcome is Unknown, as a success");
    }

    /// <summary>An attach by a human is sent; the call needs no client id.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_human_attach_is_sent()
    {
        var writes = Create();

        _ = ContractCheck.Ok(_owner, await writes.Attach(IssueKey, _url, "PR 7", Scope(ContextCallerKind.Human), CancellationToken.None));

        ContractCheck.That(_owner, WritesSent(writes) == 1, "one write went out");
    }

    /// <summary>An attach to a sindri item link is sent too, and a title of 255 characters.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_attach_of_a_sindri_item_link_is_sent()
    {
        var writes = Create();

        _ = ContractCheck.Ok(_owner, await writes.Attach(IssueKey, new Uri("sindri://item/itm_1"), _titleMax, Scope(ContextCallerKind.Human), CancellationToken.None));

        ContractCheck.That(_owner, WritesSent(writes) == 1, "one write went out");
    }

    /// <summary>A change that sets no field is refused with its own words, and nothing is sent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_change_that_sets_no_field_is_refused_with_015_and_nothing_is_sent()
    {
        var writes = Create();

        var result = await writes.Change(IssueKey, new LinearIssueChange(), Scope(ContextCallerKind.Human), CancellationToken.None);

        ContractCheck.Fails(_owner, result, "SND-LINEAR-015");
        ContractCheck.That(_owner, result.Error.Message == "Not sent: the change sets no field. Nothing was changed.", "the message says the change sets no field");
        ContractCheck.That(_owner, Sent(writes) == 0, "an empty change sent nothing");
    }

    /// <summary>An edit of a comment that isn't on the issue is refused with 005, and no write goes out.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_edit_of_a_comment_on_another_issue_is_refused_with_005_and_nothing_is_sent()
    {
        var writes = Create();

        var result = await writes.EditComment(IssueKey, CommentOnAnotherIssue(writes), "hello", Scope(ContextCallerKind.Human), CancellationToken.None);

        ContractCheck.Fails(_owner, result, "SND-LINEAR-005");
        ContractCheck.That(_owner, WritesSent(writes) == 0, "an edit of a comment on another issue sent no write");
    }
}
