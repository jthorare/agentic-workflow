using System.Net;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Infrastructure.Linear;

namespace Aw.Context.Tests.Linear;

/// <summary>What every call to Linear shares whichever key sends it (Linear hub spec 2026-11-04 §5.2): a reply becomes the data or the catalogue's error, and a failed operation is logged by name, status and code.</summary>
[Trait("Lane", "unit")]
public sealed class LinearCallsTests
{
    private const string Fix = "Make a key that can do the thing.";
    private readonly CapturingLogger<LinearClient> _log = new();
    private readonly LinearCalls _calls;

    public LinearCallsTests()
    {
        var settings = LinearSettings.Create("http://127.0.0.1:7777/graphql", "env:LINEAR_CALLS_KEY", ["ENG"]).Value;
        _calls = new LinearCalls(settings, _log, Fix);
    }

    private static byte[] Json(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void A_reply_with_data_is_the_data()
    {
        var read = _calls.Read<LinearIssueData>("Op", HttpStatusCode.OK, Json("""{"data":{"issue":{"id":"i1","identifier":"E-1","title":"t","updatedAt":"2026-01-01T00:00:00.000Z"}}}"""));

        Assert.True(read.IsSuccess);
        Assert.Equal("E-1", read.Value.Issue?.Identifier);
        Assert.Empty(_log.Lines);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void A_refused_key_is_071_and_carries_the_callers_advice(HttpStatusCode status)
    {
        var read = _calls.Read<LinearIssueData>("Op", status, Json("{}"));

        Assert.Equal(ContextErrors.LinearKeyRefused.Code, read.Error.Code);
        Assert.Contains(Fix, read.Error.Message, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:7777", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_authentication_error_inside_a_200_is_a_refused_key()
    {
        var read = _calls.Read<LinearIssueData>("Op", HttpStatusCode.OK, Json("""{"errors":[{"message":"nope","extensions":{"code":"AUTHENTICATION_ERROR"}}]}"""));

        Assert.Equal(ContextErrors.LinearKeyRefused.Code, read.Error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public void A_rate_limit_and_a_5xx_are_010_so_the_source_is_down_for_now(HttpStatusCode status)
    {
        var read = _calls.Read<LinearIssueData>("Op", status, Json("{}"));

        Assert.Equal(ContextErrors.SourceUnavailable.Code, read.Error.Code);
    }

    [Fact]
    public void A_ratelimited_graphql_error_is_010_too()
    {
        var read = _calls.Read<LinearIssueData>("Op", HttpStatusCode.BadRequest, Json("""{"errors":[{"message":"slow down","extensions":{"code":"RATELIMITED"}}]}"""));

        Assert.Equal(ContextErrors.SourceUnavailable.Code, read.Error.Code);
        Assert.Contains("rate limited", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entity_not_found_error_is_073()
    {
        var read = _calls.Read<LinearIssueData>("Op", HttpStatusCode.OK, Json("""{"errors":[{"message":"Entity not found: Issue"}]}"""));

        Assert.Equal(ContextErrors.LinearRecordNotFound.Code, read.Error.Code);
    }

    [Fact]
    public void Any_other_graphql_error_is_072_and_shows_a_cut_line_of_the_message()
    {
        var filler = new string('x', 300);
        var read = _calls.Read<LinearIssueData>("Op", HttpStatusCode.OK, Json($$"""{"errors":[{"message":"bad\nfield {{filler}}"}]}"""));

        Assert.Equal(ContextErrors.LinearUnexpectedReply.Code, read.Error.Code);
        Assert.DoesNotContain('\n', read.Error.Message);
        Assert.True(read.Error.Message.Length < 400);
    }

    [Fact]
    public void A_body_that_is_not_the_shape_and_a_reply_with_no_data_are_072()
    {
        Assert.Equal(ContextErrors.LinearUnexpectedReply.Code, _calls.Read<LinearIssueData>("Op", HttpStatusCode.OK, Json("not json")).Error.Code);
        Assert.Equal(ContextErrors.LinearUnexpectedReply.Code, _calls.Read<LinearIssueData>("Op", HttpStatusCode.OK, Json("{}")).Error.Code);
    }

    [Fact]
    public void A_failed_operation_is_logged_by_name_status_and_code_and_not_by_its_body()
    {
        _ = _calls.Read<LinearIssueData>("IssuesPull", HttpStatusCode.Forbidden, Json("""{"secret":"do-not-log-me"}"""));

        var line = Assert.Single(_log.Lines);
        Assert.Contains("IssuesPull", line, StringComparison.Ordinal);
        Assert.Contains("403", line, StringComparison.Ordinal);
        Assert.Contains("SND-CONTEXT-071", line, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-log-me", line, StringComparison.Ordinal);
    }
}
