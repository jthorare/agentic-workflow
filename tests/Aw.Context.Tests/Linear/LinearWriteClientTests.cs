using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Testing.Fakes;
using Aw.Context.Testing.Linear;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The Linear write client (Linear hub spec 2026-11-04 §5.2, §8) over the stateful fake Linear: each mutation applies and answers the issue as Linear has it, the client id becomes the object's id, and every failure the fake can switch on comes back as the
/// SND-LINEAR code the spec names.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearWriteClientTests : IAsyncLifetime
{
    private const string ReadPointer = "env:LINEAR_FIXTURE_KEY";
    private const string WritePointer = "env:LINEAR_FIXTURE_WRITE_KEY";
    private const string ReadKey = "fixture-read-key";
    private const string WriteKey = "fixture-write-key";
    private readonly CapturingLogger<LinearClient> _log = new();
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
    private FakeLinear _linear = null!;
    private LinearWriteClient _client = null!;
    private LinearSettings _settings = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _linear = await FakeLinear.Start(
            seed => seed
                .Team("ENG")
                .Member("Ada Fixture")
                .Issue("ENG", "Fix the login timeout", new FakeLinearIssueSeed { Description = "Details of the timeout", State = "Todo", Priority = 2 })
                .Comment("ENG-1", "first comment"),
            ReadKey,
            WriteKey);
        _settings = SettingsFor(_linear.Url, WritePointer);
        _client = new LinearWriteClient(new LinearSourceParts(_http, Resolve, new FakeTimeProvider(), _log));
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _linear.DisposeAsync();
    }

    private static string? Resolve(string pointer) => pointer switch
    {
        ReadPointer => ReadKey,
        WritePointer => WriteKey,
        _ => null,
    };

    private static LinearSettings SettingsFor(Uri url, string? writePointer) => LinearSettings.Create(url.ToString(), ReadPointer, ["ENG"], writePointer).Value;

    private static int _clientIds;

    private static LinearClientId NewClientId() => LinearClientId.Parse(string.Create(CultureInfo.InvariantCulture, $"0e5a1b00-0000-4000-8000-{Interlocked.Increment(ref _clientIds):D12}")).Value;

    private ExternalId IssueId => ExternalId.From(_linear.Issues.Single().Id);

    private JsonObject LastInput() =>
        JsonNode.Parse(_linear.RequestBodies[^1]) is JsonObject { } body && body["variables"] is JsonObject variables && variables["input"] is JsonObject input
            ? input
            : throw new InvalidOperationException("The last request carried no input.");

    private LinearIssueDraft Draft(string title = "A new issue") => new(ExternalId.From(_linear.TeamId("ENG")), title);

    [Fact]
    public async Task Creating_an_issue_applies_it_and_answers_it_with_the_client_id_as_its_id()
    {
        var clientId = NewClientId();

        var view = (await _client.CreateIssue(_settings, Draft("Ship the thing") with { Priority = 3, Description = "Why" }, clientId, Ct)).Value;

        Assert.Equal(clientId.Value, view.Id.Value);
        Assert.Equal(("Ship the thing", "Why", 3, "ENG"), (view.Title, view.Description, view.Priority, view.TeamKey));
        Assert.Contains(_linear.Issues, i => i.Id == clientId.Value && i.Title == "Ship the thing");
    }

    [Fact]
    public async Task Creating_an_issue_with_an_assignee_answers_their_id()
    {
        var ada = ExternalId.From(_linear.MemberId("Ada Fixture"));

        var view = (await _client.CreateIssue(_settings, Draft() with { AssigneeId = ada }, NewClientId(), Ct)).Value;

        Assert.Equal(ada, view.AssigneeId);
    }

    [Fact]
    public async Task Updating_an_issue_sends_only_the_fields_that_are_set_and_answers_the_issue_after_the_write()
    {
        var change = new LinearIssueChange(Title: LinearFieldChange.Set(value: "Renamed"), Priority: LinearFieldChange.Set(value: 4));

        var view = (await _client.UpdateIssue(_settings, IssueId, change, Ct)).Value;

        Assert.Equal(("Renamed", 4, "Details of the timeout"), (view.Title, view.Priority, view.Description));
        Assert.Equal(["priority", "title"], LastInput().Select(p => p.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Updating_a_field_to_null_clears_it()
    {
        var view = (await _client.UpdateIssue(_settings, IssueId, new LinearIssueChange(Description: LinearFieldChange.Set<string?>(value: null)), Ct)).Value;

        Assert.Null(view.Description);
        Assert.True(LastInput().ContainsKey("description"));
        Assert.Null(LastInput()["description"]);
    }

    [Fact]
    public async Task Updating_the_state_moves_the_issue_to_it()
    {
        var done = ExternalId.From(_linear.StateId("ENG", "Done"));

        var view = (await _client.UpdateIssue(_settings, IssueId, new LinearIssueChange(StateId: LinearFieldChange.Set(value: done)), Ct)).Value;

        Assert.Equal((done, "Done"), (view.StateId, view.StateName));
    }

    [Fact]
    public async Task Commenting_applies_the_comment_with_the_client_id_and_answers_its_issue()
    {
        var clientId = NewClientId();

        var view = (await _client.CreateComment(_settings, IssueId, "Looks right", clientId, Ct)).Value;

        Assert.Equal(("ENG-1", IssueId), (view.Key.ToString(), view.Id));
        Assert.Contains(_linear.Comments, c => c.Id == clientId.Value && c.Body == "Looks right");
    }

    [Fact]
    public async Task Editing_a_comment_replaces_its_text_and_answers_its_issue()
    {
        var comment = _linear.Comments.Single();

        var view = (await _client.UpdateComment(_settings, ExternalId.From(comment.Id), "Edited", Ct)).Value;

        Assert.Equal(IssueId, view.Id);
        Assert.Equal("Edited", _linear.Comments.Single().Body);
    }

    [Fact]
    public async Task Linking_a_url_adds_one_attachment_and_linking_it_again_adds_none()
    {
        var url = new Uri("https://github.com/jthorare/agentic-workflow/pull/2077");

        var first = (await _client.LinkUrl(_settings, IssueId, url, "PR 2077", Ct)).Value;
        var again = (await _client.LinkUrl(_settings, IssueId, url, "PR 2077, renamed", Ct)).Value;

        Assert.Equal(IssueId, first.Id);
        Assert.Equal(IssueId, again.Id);
        Assert.Equal(("PR 2077, renamed", 1), (_linear.Attachments.Single().Title, _linear.Attachments.Count));
    }

    [Fact]
    public async Task A_revoked_write_key_is_004()
    {
        _linear.Switches.RevokeKey = true;

        var result = await _client.UpdateIssue(_settings, IssueId, new LinearIssueChange(Title: LinearFieldChange.Set(value: "x")), Ct);

        Assert.Equal("SND-LINEAR-004", result.Error.Code);
        Assert.DoesNotContain(WriteKey, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LinearRateLimit.Http429)]
    [InlineData(LinearRateLimit.GraphQl)]
    public async Task A_rate_limit_is_012_and_names_the_time_the_window_resets_in_utc(LinearRateLimit limit)
    {
        _linear.Switches.RateLimit = limit;

        var result = await _client.CreateIssue(_settings, Draft(), NewClientId(), Ct);

        Assert.Equal("SND-LINEAR-012", result.Error.Code);
        Assert.Equal("Linear's rate limit: try again after 01:00 UTC. Nothing was changed.", result.Error.Message);
    }

    [Fact]
    public async Task A_write_the_connection_dropped_on_is_011_and_the_fake_shows_it_applied()
    {
        _linear.Switches.NoAnswer = true;
        var clientId = NewClientId();

        var result = await _client.CreateIssue(_settings, Draft("Dropped"), clientId, Ct);

        Assert.Equal("SND-LINEAR-011", result.Error.Code);
        Assert.Contains(_linear.Issues, i => i.Id == clientId.Value);
    }

    [Fact]
    public async Task A_repeated_client_id_is_015_with_linears_own_words_and_nothing_is_created()
    {
        _linear.Switches.RepeatClientId = true;
        var before = _linear.Issues.Count;

        var result = await _client.CreateIssue(_settings, Draft(), NewClientId(), Ct);

        Assert.Equal("SND-LINEAR-015", result.Error.Code);
        Assert.Contains("is already used; a client id must be unique", result.Error.Message, StringComparison.Ordinal);
        Assert.EndsWith("Nothing was changed.", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(before, _linear.Issues.Count);
    }

    [Fact]
    public async Task An_issue_that_does_not_exist_is_005()
    {
        var result = await _client.UpdateIssue(_settings, ExternalId.From("no-such-issue"), new LinearIssueChange(Title: LinearFieldChange.Set(value: "x")), Ct);

        Assert.Equal("SND-LINEAR-005", result.Error.Code);
    }

    [Fact]
    public async Task A_comment_that_does_not_exist_is_005()
    {
        var result = await _client.UpdateComment(_settings, ExternalId.From("no-such-comment"), "x", Ct);

        Assert.Equal("SND-LINEAR-005", result.Error.Code);
    }

    [Fact]
    public async Task A_linear_nobody_listens_at_is_010_so_nothing_was_changed()
    {
        await using var gone = await FakeLinear.Start();
        var url = gone.Url;
        await gone.DisposeAsync();

        var result = await _client.CreateIssue(SettingsFor(url, WritePointer), new LinearIssueDraft(ExternalId.From("team"), "x"), NewClientId(), Ct);

        Assert.Equal("SND-LINEAR-010", result.Error.Code);
    }

    [Fact]
    public async Task Settings_with_no_write_key_are_002_and_nothing_is_sent()
    {
        var result = await _client.CreateIssue(SettingsFor(_linear.Url, writePointer: null), Draft(), NewClientId(), Ct);

        Assert.Equal("SND-LINEAR-002", result.Error.Code);
        Assert.Empty(_linear.RequestBodies);
    }

    [Fact]
    public async Task A_write_pointer_that_does_not_resolve_is_002_naming_its_scheme_and_doctor_and_nothing_is_sent()
    {
        var result = await _client.CreateIssue(SettingsFor(_linear.Url, "env:LINEAR_NOT_SET"), Draft(), NewClientId(), Ct);

        Assert.Equal("SND-LINEAR-002", result.Error.Code);
        Assert.Contains("env:", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("sindri doctor", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_linear.RequestBodies);
    }

    [Fact]
    public async Task The_write_key_never_reaches_the_log()
    {
        _linear.Switches.RevokeKey = true;

        _ = await _client.CreateIssue(_settings, Draft(), NewClientId(), Ct);

        Assert.NotEmpty(_log.Lines);
        Assert.DoesNotContain(_log.Lines, l => l.Contains(WriteKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_write_that_cannot_resolve_its_own_key_does_not_fall_back_to_the_read_key()
    {
        var onlyReadResolves = new LinearWriteClient(new LinearSourceParts(_http, p => p == ReadPointer ? ReadKey : null, new FakeTimeProvider(), _log));

        var result = await onlyReadResolves.CreateIssue(_settings, Draft(), NewClientId(), Ct);

        Assert.Equal("SND-LINEAR-002", result.Error.Code);
        Assert.Empty(_linear.RequestBodies);
    }
}
