using System.Net;
using System.Text;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Linear;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>How the write client reads Linear's replies that the fake can't be made to give (a 5xx, success: false, errors with or without a landed payload, a body of the wrong shape), and what it puts on the wire. A canned reply stands in for Linear.</summary>
[Trait("Lane", "unit")]
public sealed class LinearWriteClientReplyTests : IDisposable
{
    private const string WritePointer = "env:LINEAR_FIXTURE_WRITE_KEY";
    private const string WriteKey = "fixture-write-key";
    private const string IssueJson =
        """{"id":"i1","identifier":"ENG-1","title":"t","description":null,"updatedAt":"2026-01-01T00:00:00.000Z","priority":0,"estimate":null,"state":{"id":"s1","name":"Todo","type":"unstarted"},"team":{"id":"t1","key":"ENG"},"assignee":null,"cycle":null,"project":null,"parent":null,"labels":{"nodes":[{"id":"l1"}]}}""";

    private readonly CapturingLogger<LinearClient> _log = new();
    private readonly CannedHandler _handler = new();
    private readonly HttpClient _http;
    private readonly LinearSettings _settings = LinearSettings.Create("http://127.0.0.1:7777/graphql", "env:LINEAR_FIXTURE_KEY", ["ENG"], WritePointer).Value;
    private readonly LinearWriteClient _client;

    public LinearWriteClientReplyTests()
    {
        _http = new HttpClient(_handler);
        _client = new LinearWriteClient(new LinearSourceParts(_http, p => p == WritePointer ? WriteKey : null, new FakeTimeProvider(), _log));
    }

    public void Dispose() => _http.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Applied(string issue, string errors = "") =>
        """{"data":{"issueUpdate":{"success":true,"issue":ISSUE}}ERRORS}""".Replace("ISSUE", issue, StringComparison.Ordinal).Replace("ERRORS", errors, StringComparison.Ordinal);

    private ValueTask<Result<LinearIssueView>> Update() =>
        _client.UpdateIssue(_settings, ExternalId.From("i1"), new LinearIssueChange(Title: LinearFieldChange.Set(value: "x")), Ct);

    [Fact]
    public async Task A_payload_with_the_issue_is_the_issue_view()
    {
        _handler.Reply(HttpStatusCode.OK, Applied(IssueJson));

        var view = (await Update()).Value;

        Assert.Equal(("ENG-1", "Todo", "ENG"), (view.Key.ToString(), view.StateName, view.TeamKey));
        Assert.Equal(["l1"], view.LabelIds.Select(l => l.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_write_key_goes_into_the_authorization_header_as_it_is_and_nowhere_else()
    {
        _handler.Reply(HttpStatusCode.OK, Applied(IssueJson));

        _ = await Update();

        Assert.Equal(WriteKey, _handler.Authorization);
        Assert.DoesNotContain(WriteKey, _handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(_log.Lines, l => l.Contains(WriteKey, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_401_or_403_is_004(HttpStatusCode status)
    {
        _handler.Reply(status, "{}");

        Assert.Equal("SND-LINEAR-004", (await Update()).Error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task A_5xx_is_011_because_the_write_may_have_been_applied(HttpStatusCode status)
    {
        _handler.Reply(status, "oops");

        Assert.Equal("SND-LINEAR-011", (await Update()).Error.Code);
    }

    [Fact]
    public async Task A_rate_limit_without_a_reset_header_is_012_with_no_time()
    {
        _handler.Reply(HttpStatusCode.TooManyRequests, "{}");

        var error = (await Update()).Error;

        Assert.Equal("SND-LINEAR-012", error.Code);
        Assert.Contains("try again after a minute", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rate_limit_with_a_reset_header_carries_retry_after_in_round_trip_utc_form()
    {
        _handler.Reply(HttpStatusCode.TooManyRequests, "{}");
        _handler.ResetHeader = "1767261960000";

        var error = (await Update()).Error;

        Assert.Equal("SND-LINEAR-012", error.Code);
        var extension = Assert.Single(error.Extensions!);
        Assert.Equal("retryAfter", extension.Key);
        Assert.Equal("2026-01-01T10:06:00.0000000Z", extension.Value.GetString());
    }

    [Fact]
    public async Task A_rate_limit_without_a_reset_header_has_no_retry_after()
    {
        _handler.Reply(HttpStatusCode.TooManyRequests, "{}");

        var error = (await Update()).Error;

        Assert.True(error.Extensions is null || error.Extensions.Count == 0);
    }

    [Fact]
    public async Task Success_false_is_015_even_in_a_200()
    {
        _handler.Reply(HttpStatusCode.OK, """{"data":{"issueUpdate":{"success":false,"issue":null}}}""");

        var error = (await Update()).Error;

        Assert.Equal("SND-LINEAR-015", error.Code);
        Assert.EndsWith("Nothing was changed.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Errors_without_a_landed_payload_are_015_and_a_user_presentable_message_beats_the_plain_one()
    {
        _handler.Reply(HttpStatusCode.OK, """{"data":{"issueUpdate":null},"errors":[{"message":"plain","extensions":{"code":"INVALID_INPUT","userPresentableMessage":"The title is too long."}}]}""");

        var error = (await Update()).Error;

        Assert.Equal("SND-LINEAR-015", error.Code);
        Assert.Equal("Linear said no: The title is too long. Nothing was changed.", error.Message);
    }

    [Fact]
    public async Task Errors_next_to_success_true_are_011_because_the_write_may_have_landed()
    {
        _handler.Reply(HttpStatusCode.OK, Applied(IssueJson, ""","errors":[{"message":"Cannot return null for non-nullable field Issue.cycle","path":["issueUpdate","issue","cycle"]}]"""));

        var error = (await Update()).Error;

        Assert.Equal("SND-LINEAR-011", error.Code);
        Assert.Contains("may or may not have been applied", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task What_linear_said_is_cut_to_200_characters_with_control_characters_removed()
    {
        _handler.Reply(HttpStatusCode.OK, """{"errors":[{"message":"bad\u0007\nXXX"}]}""".Replace("XXX", new string('x', 300), StringComparison.Ordinal));

        var error = (await Update()).Error;

        Assert.Equal("Linear said no: bad" + new string('x', 197) + ". Nothing was changed.", error.Message);
    }

    [Fact]
    public async Task A_missing_entity_is_005_even_when_linear_says_it_in_a_200()
    {
        _handler.Reply(HttpStatusCode.OK, """{"data":{"issueUpdate":null},"errors":[{"message":"Entity not found: Issue"}]}""");

        Assert.Equal("SND-LINEAR-005", (await Update()).Error.Code);
    }

    [Theory]
    [InlineData("""{"data":{"issueUpdate":null}}""")]
    [InlineData("""{"data":{"somethingElse":1}}""")]
    [InlineData("""{"data":{"issueUpdate":{"success":true,"issue":null}}}""")]
    [InlineData("not json")]
    public async Task A_body_of_the_wrong_shape_is_context_072(string body)
    {
        _handler.Reply(HttpStatusCode.OK, body);

        Assert.Equal("SND-CONTEXT-072", (await Update()).Error.Code);
    }

    [Fact]
    public async Task An_issue_with_an_identifier_that_is_not_a_key_is_context_072()
    {
        _handler.Reply(HttpStatusCode.OK, Applied(IssueJson.Replace("ENG-1", "nonsense", StringComparison.Ordinal)));

        Assert.Equal("SND-CONTEXT-072", (await Update()).Error.Code);
    }

    private sealed class CannedHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _reply = "{}";

        public string? Authorization { get; private set; }

        public string Body { get; private set; } = "";

        public string? ResetHeader { get; set; }

        public void Reply(HttpStatusCode status, string body)
        {
            _status = status;
            _reply = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.TryGetValues("Authorization", out var values) ? values.Single() : null;
            Body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken) : "";
            var response = new HttpResponseMessage(_status) { Content = new StringContent(_reply, Encoding.UTF8, "application/json") };
            if (ResetHeader is not null)
            {
                response.Headers.Add("x-ratelimit-requests-reset", ResetHeader);
            }

            return response;
        }
    }
}
