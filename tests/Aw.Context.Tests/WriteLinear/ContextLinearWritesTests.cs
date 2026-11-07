using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Features.WriteLinear;
using Aw.Kernel;

namespace Aw.Context.Tests.WriteLinear;

/// <summary>
/// The Linear writes' order and outcomes (Linear hub spec 2026-11-04 §5.2, §5.5) over hand-written ports: the caller, the settings, the local checks, the live read, the mutation, the write-through, and the one re-read after a write got no answer.
/// </summary>
[Trait("Lane", "unit")]
public sealed class ContextLinearWritesTests
{
    private static readonly LinearIssueKey _key = LinearIssueKey.Parse("ENG-12").Value;
    private static readonly LinearClientId _clientId = LinearClientId.Parse("0f8fad5b-d9cb-469f-a165-70867728950e").Value;
    private static readonly ExternalId _issueId = ExternalId.From("issue-12");
    private static readonly ExternalId _commentId = ExternalId.From("comment-1");
    private static readonly ExternalId _done = ExternalId.From("state-done");
    private static readonly ExternalId _ada = ExternalId.From("user-ada");
    private static readonly Uri _url = new("https://github.com/acme/app/pull/7");
    private static readonly JsonElement _linear = JsonDocument.Parse("""{"sources":{"linear":{"url":"http://127.0.0.1:9/graphql","teams":["ENG"],"token":"env:LINEAR_KEY"}}}""").RootElement.Clone();
    private static readonly JsonElement _noLinear = JsonDocument.Parse("""{"sources":{}}""").RootElement.Clone();

    private readonly ScriptedWrites _writes = new();
    private readonly ScriptedReads _reads = new();
    private readonly RecordedThrough _through = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ContextLinearWrites Facade() => new(_writes, _reads, _through);

    private static ContextScope Human(JsonElement? settings = null) => new(settings ?? _linear, [], new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    private static ContextScope Session() => new(_linear, [], new ContextCaller(ContextCallerKind.Session, ContextRepoName.From("split-panes"), ContextClient.Cli));

    private static LinearIssueView View(string title = "Fix the login timeout", ExternalId? state = null, ExternalId? assignee = null, IReadOnlyList<ExternalId>? labels = null) => new(
        _issueId, _key, "ENG", title, Description: null, state ?? ExternalId.From("state-todo"), "Todo", assignee, Priority: 0, Estimate: null, CycleId: null, ProjectId: null, ParentId: null, labels ?? [], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static LinearIssueChange Title(string title) => new(Title: LinearFieldChange.Set(title));

    private static void AssertFails<T>(Result<T> result, string code)
    {
        Assert.True(result.IsError, "expected an error");
        Assert.Equal(code, result.Error.Code);
    }

    [Fact]
    public async Task A_session_is_refused_before_the_settings_are_read_and_nothing_is_read_or_sent()
    {
        var facade = Facade();
        var noSettings = new ContextScope(Settings: null, [], new ContextCaller(ContextCallerKind.Session, ContextRepoName.From("split-panes"), ContextClient.Cli));

        AssertFails(await facade.Change(_key, Title("New"), noSettings, Ct), "SND-CONTEXT-020");
        AssertFails(await facade.Create(new LinearIssueDraft(ExternalId.From("team"), "A"), _clientId, Session(), Ct), "SND-CONTEXT-020");
        AssertFails(await facade.Live(_key, Session(), Ct), "SND-CONTEXT-020");
        AssertFails(await facade.WriteKeyStatus(Session(), Ct), "SND-CONTEXT-020");
        Assert.Empty(_reads.Calls);
        Assert.Empty(_writes.Calls);
        Assert.Empty(_through.Stored);
    }

    [Fact]
    public async Task A_profile_without_linear_is_SND_CONTEXT_002_and_nothing_is_read_or_sent()
    {
        var facade = Facade();
        var scope = Human(_noLinear);

        AssertFails(await facade.Comment(_key, "hi", _clientId, scope, Ct), "SND-CONTEXT-002");
        AssertFails(await facade.Live(_key, scope, Ct), "SND-CONTEXT-002");
        AssertFails(await facade.WriteKeyStatus(scope, Ct), "SND-CONTEXT-002");
        Assert.Empty(_reads.Calls);
        Assert.Empty(_writes.Calls);
    }

    [Fact]
    public async Task A_local_refusal_is_015_before_the_issue_is_read()
    {
        var facade = Facade();

        AssertFails(await facade.Comment(_key, "", _clientId, Human(), Ct), "SND-LINEAR-015");
        AssertFails(await facade.Change(_key, new LinearIssueChange(), Human(), Ct), "SND-LINEAR-015");
        AssertFails(await facade.Attach(_key, new Uri("http://example.com/x"), "t", Human(), Ct), "SND-LINEAR-015");
        AssertFails(await facade.Create(new LinearIssueDraft(ExternalId.From("team"), " "), _clientId, Human(), Ct), "SND-LINEAR-015");
        Assert.Empty(_reads.Calls);
        Assert.Empty(_writes.Calls);
    }

    [Fact]
    public async Task A_missing_issue_is_005_with_the_hint_and_nothing_is_sent()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(value: null);

        var result = await Facade().Change(_key, Title("New"), Human(), Ct);

        AssertFails(result, "SND-LINEAR-005");
        Assert.Equal("No issue ENG-12 in your teams (deleted, moved, or not synced yet: `sindri linear show` after the next tick).", result.Error.Message);
        Assert.Empty(_writes.Calls);
    }

    [Fact]
    public async Task An_unreachable_read_is_010_and_another_read_error_passes_through()
    {
        _reads.Issues["ENG-12"] = ContextErrors.SourceUnavailable.With("Linear at x isn't reachable.");
        AssertFails(await Facade().Change(_key, Title("New"), Human(), Ct), "SND-LINEAR-010");

        _reads.Issues["ENG-12"] = ContextErrors.LinearKeyRefused.With("refused");
        AssertFails(await Facade().Comment(_key, "hi", _clientId, Human(), Ct), ContextErrors.LinearKeyRefused.Code);
        Assert.Empty(_writes.Calls);
    }

    [Fact]
    public async Task Live_reads_the_issue_by_its_key_and_sends_no_mutation()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());

        var result = await Facade().Live(_key, Human(), Ct);

        Assert.Equal(_issueId, result.Value.Id);
        Assert.Empty(_writes.Calls);
        AssertFails(await Facade().Live(LinearIssueKey.Parse("ENG-99").Value, Human(), Ct), "SND-LINEAR-005");
    }

    [Fact]
    public async Task WriteKeyStatus_asks_the_write_port_and_reads_nothing_from_linear()
    {
        _writes.KeyStatusAnswer = new LinearWriteKeyNotSet();

        var result = await Facade().WriteKeyStatus(Human(), Ct);

        _ = Assert.IsType<LinearWriteKeyNotSet>(result.Value);
        Assert.Empty(_reads.Calls);
        Assert.Equal(["KeyStatus"], _writes.Calls);
    }

    [Fact]
    public async Task A_change_resolves_the_issue_sends_the_fields_and_stores_the_answer_as_applied()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        var answer = View("New title");
        _writes.Answer = answer;

        var result = await Facade().Change(_key, Title("New title"), Human(), Ct);

        Assert.Equal(answer, Assert.IsType<LinearWriteApplied>(result.Value).Stored);
        Assert.Equal(["UpdateIssue:issue-12"], _writes.Calls);
        Assert.Equal([(_issueId, LinearWrittenFields.None)], _through.Stored);
    }

    [Theory]
    [InlineData(true, false, LinearWrittenFields.Assignee)]
    [InlineData(false, true, LinearWrittenFields.State)]
    [InlineData(true, true, LinearWrittenFields.Assignee | LinearWrittenFields.State)]
    public async Task The_write_through_is_told_which_of_assignee_and_state_the_change_sent(bool assignee, bool state, LinearWrittenFields expected)
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        _writes.Answer = View();
        var change = new LinearIssueChange(AssigneeId: assignee ? LinearFieldChange.Set<ExternalId?>(_ada) : default, StateId: state ? LinearFieldChange.Set(_done) : default);

        _ = await Facade().Change(_key, change, Human(), Ct);

        Assert.Equal([(_issueId, expected)], _through.Stored);
    }

    [Fact]
    public async Task A_create_with_a_state_and_an_assignee_stores_both_flags_and_needs_no_live_read()
    {
        _writes.Answer = View();

        var result = await Facade().Create(new LinearIssueDraft(ExternalId.From("team"), "A", StateId: _done, AssigneeId: _ada), _clientId, Human(), Ct);

        _ = Assert.IsType<LinearWriteApplied>(result.Value);
        Assert.Empty(_reads.Calls);
        Assert.Equal([(_issueId, LinearWrittenFields.Assignee | LinearWrittenFields.State)], _through.Stored);
    }

    [Fact]
    public async Task A_failed_write_through_after_the_mutation_is_applied_not_stored_never_an_error()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        var answer = View("New title");
        _writes.Answer = answer;
        _through.Succeeds = false;

        var result = await Facade().Change(_key, Title("New title"), Human(), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(answer, Assert.IsType<LinearWriteAppliedNotStored>(result.Value).Live);
    }

    [Theory]
    [InlineData("SND-LINEAR-004")]
    [InlineData("SND-LINEAR-012")]
    [InlineData("SND-LINEAR-015")]
    [InlineData("SND-LINEAR-005")]
    public async Task A_refusal_of_the_mutation_passes_through_and_nothing_is_stored(string code)
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        _writes.Answer = new Error(code, ErrorKind.Invalid, "refused");

        AssertFails(await Facade().Change(_key, Title("New"), Human(), Ct), code);
        Assert.Empty(_through.Stored);
    }

    [Fact]
    public async Task A_create_with_no_answer_is_found_by_its_client_id_and_stored()
    {
        _writes.Answer = LinearHubErrors.NoAnswer.With("timeout");
        _reads.Issues[_clientId.Value] = Results.Success<LinearIssueView?>(View());

        var result = await Facade().Create(new LinearIssueDraft(ExternalId.From("team"), "A"), _clientId, Human(), Ct);

        _ = Assert.IsType<LinearWriteApplied>(result.Value);
        Assert.Equal([_clientId.Value], _reads.Calls);
        _ = Assert.Single(_through.Stored);
    }

    [Fact]
    public async Task A_create_with_no_answer_that_is_not_found_is_unknown_and_stores_nothing()
    {
        _writes.Answer = LinearHubErrors.NoAnswer.With("timeout");
        _reads.Issues[_clientId.Value] = Results.Success<LinearIssueView?>(value: null);

        var result = await Facade().Create(new LinearIssueDraft(ExternalId.From("team"), "A"), _clientId, Human(), Ct);

        _ = Assert.IsType<LinearWriteUnknown>(result.Value);
        Assert.Empty(_through.Stored);
    }

    [Fact]
    public async Task A_re_read_that_fails_is_unknown_too_and_is_tried_once()
    {
        _writes.Answer = LinearHubErrors.NoAnswer.With("timeout");
        _reads.Issues[_clientId.Value] = ContextErrors.SourceUnavailable.With("down");

        var result = await Facade().Create(new LinearIssueDraft(ExternalId.From("team"), "A"), _clientId, Human(), Ct);

        _ = Assert.IsType<LinearWriteUnknown>(result.Value);
        _ = Assert.Single(_reads.Calls);
    }

    [Fact]
    public async Task A_change_with_no_answer_is_applied_when_every_sent_field_holds_its_new_value()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        _writes.Answer = LinearHubErrors.NoAnswer.With("timeout");
        var change = new LinearIssueChange(Title: LinearFieldChange.Set("New title"), StateId: LinearFieldChange.Set(_done), LabelIds: LinearFieldChange.Set<IReadOnlyList<ExternalId>>([_ada, _done]));
        _reads.Issues[_issueId.Value] = Results.Success<LinearIssueView?>(View("New title", state: _done, labels: [_done, _ada]));

        var result = await Facade().Change(_key, change, Human(), Ct);

        _ = Assert.IsType<LinearWriteApplied>(result.Value);
        Assert.Equal(LinearWrittenFields.State, Assert.Single(_through.Stored).Written);
    }

    [Fact]
    public async Task A_change_with_no_answer_is_unknown_when_one_sent_field_is_still_old()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        _writes.Answer = LinearHubErrors.NoAnswer.With("timeout");
        _reads.Issues[_issueId.Value] = Results.Success<LinearIssueView?>(View("New title"));

        var result = await Facade().Change(_key, new LinearIssueChange(Title: LinearFieldChange.Set("New title"), StateId: LinearFieldChange.Set(_done)), Human(), Ct);

        _ = Assert.IsType<LinearWriteUnknown>(result.Value);
        Assert.Empty(_through.Stored);
    }

    [Fact]
    public async Task A_comment_with_no_answer_is_found_by_its_client_id_on_the_same_issue()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        _writes.Answer = LinearHubErrors.NoAnswer.With("timeout");
        _reads.Comments[_clientId.Value] = Results.Success<LinearCommentView?>(new LinearCommentView(ExternalId.From(_clientId.Value), _issueId, "hi"));

        var found = await Facade().Comment(_key, "hi", _clientId, Human(), Ct);

        _ = Assert.IsType<LinearWriteApplied>(found.Value);
        _reads.Comments[_clientId.Value] = Results.Success<LinearCommentView?>(value: null);
        _through.Stored.Clear();
        var missing = await Facade().Comment(_key, "hi", _clientId, Human(), Ct);
        _ = Assert.IsType<LinearWriteUnknown>(missing.Value);
        Assert.Empty(_through.Stored);
    }

    [Fact]
    public async Task An_edit_with_no_answer_is_applied_when_the_comment_holds_the_new_text()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        _writes.Answer = LinearHubErrors.NoAnswer.With("timeout");
        _reads.Comments[_commentId.Value] = Results.Success<LinearCommentView?>(new LinearCommentView(_commentId, _issueId, "new text"));

        var applied = await Facade().EditComment(_key, _commentId, "new text", Human(), Ct);
        var unknown = await Facade().EditComment(_key, _commentId, "another text", Human(), Ct);

        _ = Assert.IsType<LinearWriteApplied>(applied.Value);
        _ = Assert.IsType<LinearWriteUnknown>(unknown.Value);
    }

    [Fact]
    public async Task An_attach_with_no_answer_is_applied_when_the_issue_has_the_url()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        _writes.Answer = LinearHubErrors.NoAnswer.With("timeout");
        _reads.HasUrl = Results.Success(value: true);

        var applied = await Facade().Attach(_key, _url, "PR 7", Human(), Ct);
        _reads.HasUrl = Results.Success(value: false);
        var unknown = await Facade().Attach(_key, _url, "PR 7", Human(), Ct);
        _reads.HasUrl = ContextErrors.SourceUnavailable.With("down");
        var failed = await Facade().Attach(_key, _url, "PR 7", Human(), Ct);

        _ = Assert.IsType<LinearWriteApplied>(applied.Value);
        _ = Assert.IsType<LinearWriteUnknown>(unknown.Value);
        _ = Assert.IsType<LinearWriteUnknown>(failed.Value);
    }

    [Fact]
    public async Task A_comment_that_was_answered_is_stored_without_a_re_read()
    {
        _reads.Issues["ENG-12"] = Results.Success<LinearIssueView?>(View());
        _writes.Answer = View();

        var result = await Facade().Comment(_key, "hi", _clientId, Human(), Ct);

        _ = Assert.IsType<LinearWriteApplied>(result.Value);
        Assert.Equal(["ENG-12"], _reads.Calls);
        Assert.Equal(["CreateComment:issue-12"], _writes.Calls);
    }

    private sealed class ScriptedWrites : ILinearWrites
    {
        public List<string> Calls { get; } = [];

        public LinearWriteKeyStatus KeyStatusAnswer { get; set; } = new LinearWriteKeyResolves();

        public Result<LinearIssueView> Answer { get; set; } = View();

        public ValueTask<LinearWriteKeyStatus> KeyStatus(LinearSettings settings, CancellationToken cancellationToken)
        {
            Calls.Add("KeyStatus");
            return ValueTask.FromResult(KeyStatusAnswer);
        }

        public ValueTask<Result<LinearIssueView>> CreateIssue(LinearSettings settings, LinearIssueDraft draft, LinearClientId clientId, CancellationToken cancellationToken) => Record("CreateIssue");

        public ValueTask<Result<LinearIssueView>> UpdateIssue(LinearSettings settings, ExternalId issueId, LinearIssueChange change, CancellationToken cancellationToken) => Record("UpdateIssue:" + issueId.Value);

        public ValueTask<Result<LinearIssueView>> CreateComment(LinearSettings settings, ExternalId issueId, string body, LinearClientId clientId, CancellationToken cancellationToken) => Record("CreateComment:" + issueId.Value);

        public ValueTask<Result<LinearIssueView>> UpdateComment(LinearSettings settings, ExternalId commentId, string body, CancellationToken cancellationToken) => Record("UpdateComment:" + commentId.Value);

        public ValueTask<Result<LinearIssueView>> LinkUrl(LinearSettings settings, ExternalId issueId, Uri url, string title, CancellationToken cancellationToken) => Record("LinkUrl:" + issueId.Value);

        private ValueTask<Result<LinearIssueView>> Record(string call)
        {
            Calls.Add(call);
            return ValueTask.FromResult(Answer);
        }
    }

    private sealed class ScriptedReads : ILinearLiveReads
    {
        public List<string> Calls { get; } = [];

        public Dictionary<string, Result<LinearIssueView?>> Issues { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Result<LinearCommentView?>> Comments { get; } = new(StringComparer.Ordinal);

        public Result<bool> HasUrl { get; set; } = Results.Success(value: false);

        public ValueTask<Result<LinearIssueView?>> Issue(LinearSettings settings, string idOrKey, CancellationToken cancellationToken)
        {
            Calls.Add(idOrKey);
            return ValueTask.FromResult(Issues.TryGetValue(idOrKey, out var answer) ? answer : Results.Success<LinearIssueView?>(value: null));
        }

        public ValueTask<Result<LinearCommentView?>> Comment(LinearSettings settings, ExternalId commentId, CancellationToken cancellationToken)
        {
            Calls.Add(commentId.Value);
            return ValueTask.FromResult(Comments.TryGetValue(commentId.Value, out var answer) ? answer : Results.Success<LinearCommentView?>(value: null));
        }

        public ValueTask<Result<bool>> HasAttachment(LinearSettings settings, ExternalId issueId, Uri url, CancellationToken cancellationToken)
        {
            Calls.Add("HasAttachment:" + issueId.Value);
            return ValueTask.FromResult(HasUrl);
        }
    }

    private sealed class RecordedThrough : ILinearWriteThrough
    {
        public List<(ExternalId Id, LinearWrittenFields Written)> Stored { get; } = [];

        public bool Succeeds { get; set; } = true;

        public ValueTask<bool> Store(ContextScope scope, ExternalId issueId, LinearWrittenFields written, CancellationToken cancellationToken)
        {
            if (Succeeds)
            {
                Stored.Add((issueId, written));
            }

            return ValueTask.FromResult(Succeeds);
        }
    }
}
