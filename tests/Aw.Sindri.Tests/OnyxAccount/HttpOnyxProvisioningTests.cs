using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxAccount;

// The ten setup-only requests (docs/sindri/onyx.md section 7), against a scripted Onyx: their shapes, the refusal of everything else, the pinned targets, and what never leaves.
[Lane(TestLane.Integration)]
public sealed class HttpOnyxProvisioningTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Sent(HttpMethod Method, string PathAndQuery, string Body, string? Authorization);

    private sealed class Script : HttpMessageHandler
    {
        public List<Sent> Requests { get; } = [];

        public Func<Sent, bool> Hang { get; set; } = static _ => false;

        public Func<Sent, bool> Refuse { get; set; } = static _ => false;

        public Func<Sent, (HttpStatusCode Status, string Body)> Answer { get; set; } = static _ => (HttpStatusCode.OK, "{}");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var sent = new Sent(request.Method, request.RequestUri!.PathAndQuery, body, request.Headers.Authorization?.ToString());
            Requests.Add(sent);
            if (Refuse(sent))
            {
                throw new HttpRequestException("connection refused");
            }

            if (Hang(sent))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            var (status, text) = Answer(sent);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static HttpOnyxProvisioning Adapter(Script script) => new(() => script);

    // A script that answers one path with (status, body) and every other with 200 {}.
    private static Script Answering(string path, HttpStatusCode status, string body)
        => new() { Answer = sent => sent.PathAndQuery == path ? (status, body) : (HttpStatusCode.OK, "{}") };

    private static async Task<IOnyxSession> Session(Script script)
        => (await Adapter(script).SignIn(3000, "a@b.c", "x", Token)).Value;

    [Theory, Trait("Lane", "integration")]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("POST", "/api/auth/logout", true)]
    [InlineData("POST", "/api/auth/register", true)]
    [InlineData("POST", "/api/password/reset_password", true)]
    [InlineData("PATCH", "/api/manage/admin/users/admin-access", true)]
    [InlineData("PATCH", "/api/manage/admin/deactivate-user", true)]
    [InlineData("GET", "/api/user/pats", true)]
    [InlineData("POST", "/api/user/pats", true)]
    [InlineData("DELETE", "/api/user/pats/7", true)]
    [InlineData("DELETE", "/api/user/pats/123456789", true)]
    [InlineData("GET", "/api/me", true)]
    [InlineData("DELETE", "/api/user/pats", false)]
    [InlineData("DELETE", "/api/user/pats/", false)]
    [InlineData("DELETE", "/api/user/pats/abc", false)]
    [InlineData("DELETE", "/api/user/pats/-1", false)]
    [InlineData("DELETE", "/api/user/pats/7/extra", false)]
    [InlineData("DELETE", "/api/user/pats/1234567890", false)]
    [InlineData("DELETE", "/api/user/pats/7%2F..", false)]
    [InlineData("GET", "/api/user/pats/7", false)]
    [InlineData("POST", "/api/user/pats/7", false)]
    [InlineData("POST", "/api/admin/api-key", false)]
    [InlineData("GET", "/api/manage/admin/user-group", false)]
    [InlineData("PATCH", "/api/manage/admin/users/craft-enabled", false)]
    [InlineData("PATCH", "/api/manage/admin/activate-user", false)]
    [InlineData("DELETE", "/api/manage/admin/delete-user", false)]
    [InlineData("PUT", "/api/manage/admin/users", false)]
    [InlineData("POST", "/api/me", false)]
    [InlineData("GET", "/api/auth/login", false)]
    [InlineData("POST", "/api/password/change-password", false)]
    public void Only_the_ten_allowlisted_requests_may_be_sent(string method, string path, bool allowed)
        => Assert.Equal(allowed, HttpOnyxProvisioning.Allowed(new HttpMethod(method), path));

    [Fact, Trait("Lane", "integration")]
    public async Task A_login_posts_a_form_and_the_session_logs_out_when_disposed()
    {
        var script = new Script();

        var session = (await Adapter(script).SignIn(3000, "owner@example.net", "pw value", Token)).Value;
        await session.DisposeAsync();

        var login = script.Requests[0];
        Assert.Equal(HttpMethod.Post, login.Method);
        Assert.Equal("/api/auth/login", login.PathAndQuery);
        Assert.Equal("username=owner%40example.net&password=pw+value", login.Body);
        Assert.Equal("/api/auth/logout", script.Requests[^1].PathAndQuery);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_logout_that_hangs_is_given_up_on_without_throwing_and_the_made_token_survives()
    {
        var script = new Script
        {
            Answer = static sent => sent.PathAndQuery == "/api/user/pats" ? (HttpStatusCode.OK, "{\"token\":\"onyx_pat_abc123\"}") : (HttpStatusCode.OK, "{}"),
            Hang = static sent => sent.PathAndQuery == "/api/auth/logout",
        };
        var adapter = new HttpOnyxProvisioning(() => script, TimeSpan.FromMilliseconds(100));
        var session = (await adapter.SignIn(3000, "a@b.c", "x", Token)).Value;
        var token = await session.CreateToken("sindri-onyx", Token);

        await session.DisposeAsync();   // must not throw

        Assert.Equal("onyx_pat_abc123", token.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_refused_login_is_sindri_onyx_008_and_does_not_repeat_the_password()
    {
        var script = new Script { Answer = static _ => (HttpStatusCode.BadRequest, "{\"detail\":\"LOGIN_BAD_CREDENTIALS\"}") };

        var result = await Adapter(script).SignIn(3000, "owner@example.net", "the-password-text", Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.DoesNotContain("the-password-text", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_dedicated_user_is_registered_with_a_json_body_naming_the_dedicated_email_only()
    {
        var script = Answering("/api/auth/register", HttpStatusCode.Created, "{\"id\":\"u1\"}");
        var session = await Session(script);

        var made = await session.RegisterDedicatedUser("pw-0123456789", Token);

        Assert.True(made.IsSuccess);
        var request = script.Requests.Single(static r => r.PathAndQuery == "/api/auth/register");
        Assert.Equal(HttpMethod.Post, request.Method);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(OnyxAccounts.DedicatedEmail, body.RootElement.GetProperty("email").GetString());
        Assert.Equal("pw-0123456789", body.RootElement.GetProperty("password").GetString());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Unauthorized, "{\"detail\":\"This workspace is invite-only.\"}", "invite {0} once")]
    [InlineData(HttpStatusCode.Forbidden, "{}", "invite {0} once")]
    [InlineData(HttpStatusCode.BadRequest, "{\"detail\":{\"code\":\"REGISTER_INVALID_PASSWORD\",\"reason\":\"Password must be 12 characters\"}}", "HTTP 400")]
    [InlineData(HttpStatusCode.BadRequest, "{\"detail\":\"Email domain is not valid\"}", "VALID_EMAIL_DOMAINS")]
    [InlineData(HttpStatusCode.InternalServerError, "{}", "HTTP 500")]
    public async Task A_registration_Onyx_refuses_is_sindri_onyx_008_that_says_what_the_owner_does_and_never_repeats_the_password(HttpStatusCode status, string body, string says)
    {
        var session = await Session(Answering("/api/auth/register", status, body));

        var made = await session.RegisterDedicatedUser("the-password-text", Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, made.Error.Code);
        Assert.Contains(string.Format(System.Globalization.CultureInfo.InvariantCulture, says, OnyxAccounts.DedicatedEmail), made.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("the-password-text", made.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_reset_names_the_dedicated_email_and_returns_the_new_password_only_to_the_caller()
    {
        var script = Answering("/api/password/reset_password", HttpStatusCode.OK, "{\"user_id\":\"u1\",\"new_password\":\"fresh-pass-77\"}");
        var session = await Session(script);

        var reset = await session.ResetDedicatedPassword(Token);

        Assert.Equal("fresh-pass-77", reset.Value);
        Assert.Equal("{\"user_email\":\"sindri@example.com\"}", script.Requests.Single(static r => r.PathAndQuery == "/api/password/reset_password").Body);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_reset_of_a_user_Onyx_does_not_have_is_null_not_an_error_so_the_caller_registers_it()
    {
        var session = await Session(Answering("/api/password/reset_password", HttpStatusCode.NotFound, "{\"detail\":\"User not found\"}"));

        var reset = await session.ResetDedicatedPassword(Token);

        Assert.True(reset.IsSuccess);
        Assert.Null(reset.Value);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Forbidden, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.Unauthorized, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.InternalServerError, "SND-ONYX-008")]
    public async Task A_reset_Onyx_does_not_do_is_007_for_a_non_admin_and_008_otherwise(HttpStatusCode status, string code)
    {
        var session = await Session(Answering("/api/password/reset_password", status, "{\"detail\":\"x\"}"));

        Assert.Equal(code, (await session.ResetDedicatedPassword(Token)).Error.Code);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not json")]
    [InlineData("{\"user_id\":\"u1\",\"new_password\":\"\"}")]
    public async Task A_reset_answer_without_a_password_is_sindri_onyx_008(string body)
    {
        var session = await Session(Answering("/api/password/reset_password", HttpStatusCode.OK, body));

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, (await session.ResetDedicatedPassword(Token)).Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Making_the_dedicated_user_an_admin_patches_the_admin_access_route_for_that_email_only()
    {
        var script = Answering("/api/manage/admin/users/admin-access", HttpStatusCode.OK, "null");
        var session = await Session(script);

        var made = await session.MakeDedicatedAdmin(Token);

        Assert.True(made.IsSuccess);
        var request = script.Requests.Single(static r => r.PathAndQuery == "/api/manage/admin/users/admin-access");
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("{\"user_email\":\"sindri@example.com\",\"is_admin\":true}", request.Body);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Forbidden, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.Unauthorized, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.NotFound, "SND-ONYX-008")]
    [InlineData(HttpStatusCode.PaymentRequired, "SND-ONYX-008")]
    public async Task A_login_that_may_not_make_admins_is_007_and_any_other_refusal_is_008(HttpStatusCode status, string code)
    {
        var session = await Session(Answering("/api/manage/admin/users/admin-access", status, "{}"));

        Assert.Equal(code, (await session.MakeDedicatedAdmin(Token)).Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_earlier_builds_user_is_deactivated_by_its_retired_email_and_a_404_means_there_is_none()
    {
        var found = Answering("/api/manage/admin/deactivate-user", HttpStatusCode.OK, "null");
        var none = Answering("/api/manage/admin/deactivate-user", HttpStatusCode.NotFound, "{\"detail\":\"User not found\"}");

        var deactivated = await (await Session(found)).RetireLegacyUser(Token);
        var absent = await (await Session(none)).RetireLegacyUser(Token);

        Assert.True(deactivated.Value);
        Assert.False(absent.Value);
        var request = found.Requests.Single(static r => r.PathAndQuery == "/api/manage/admin/deactivate-user");
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("{\"user_email\":\"sindri@sindri.app\"}", request.Body);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Forbidden, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.InternalServerError, "SND-ONYX-008")]
    public async Task A_retire_that_fails_is_007_for_a_non_admin_and_008_otherwise(HttpStatusCode status, string code)
    {
        var session = await Session(Answering("/api/manage/admin/deactivate-user", status, "{}"));

        Assert.Equal(code, (await session.RetireLegacyUser(Token)).Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Old_tokens_with_the_name_are_deleted_by_id_and_other_tokens_are_left()
    {
        var script = new Script
        {
            Answer = static sent => sent.PathAndQuery == "/api/user/pats" && sent.Method == HttpMethod.Get
                ? (HttpStatusCode.OK, "[{\"id\":4,\"name\":\"sindri-onyx\"},{\"id\":5,\"name\":\"mine\"},{\"id\":9,\"name\":\"sindri-onyx\"}]")
                : (HttpStatusCode.OK, "{}"),
        };
        var session = await Session(script);

        var revoked = await session.RevokeTokens("sindri-onyx", Token);

        Assert.Equal(2, revoked.Value);
        var deletes = script.Requests.Where(static r => r.Method == HttpMethod.Delete).Select(static r => r.PathAndQuery).ToList();
        Assert.Equal(["/api/user/pats/4", "/api/user/pats/9"], deletes);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task A_token_list_that_cannot_be_read_is_sindri_onyx_008_and_deletes_nothing(HttpStatusCode status, string body)
    {
        var script = Answering("/api/user/pats", status, body);
        var session = await Session(script);

        var revoked = await session.RevokeTokens("sindri-onyx", Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, revoked.Error.Code);
        Assert.DoesNotContain(script.Requests, static r => r.Method == HttpMethod.Delete);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_delete_that_fails_stops_the_revoke_with_sindri_onyx_008_and_a_vanished_token_is_fine()
    {
        var listed = "[{\"id\":4,\"name\":\"sindri-onyx\"}]";
        var failing = new Script { Answer = sent => sent.Method == HttpMethod.Delete ? (HttpStatusCode.InternalServerError, "{}") : (HttpStatusCode.OK, listed) };
        var vanished = new Script { Answer = sent => sent.Method == HttpMethod.Delete ? (HttpStatusCode.NotFound, "{}") : (HttpStatusCode.OK, listed) };

        var broken = await (await Session(failing)).RevokeTokens("sindri-onyx", Token);
        var gone = await (await Session(vanished)).RevokeTokens("sindri-onyx", Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, broken.Error.Code);
        Assert.Equal(1, gone.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_token_is_made_unscoped_and_without_expiry_and_comes_back_once()
    {
        var script = Answering("/api/user/pats", HttpStatusCode.OK, "{\"id\":4,\"name\":\"sindri-onyx\",\"token\":\"onyx_pat_abc123\",\"token_display\":\"onyx_pat_abc1****x9\",\"scopes\":null}");
        var session = await Session(script);

        var token = await session.CreateToken("sindri-onyx", Token);

        Assert.Equal("onyx_pat_abc123", token.Value);
        var request = script.Requests.Single(static r => r.PathAndQuery == "/api/user/pats");
        Assert.Equal(HttpMethod.Post, request.Method);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("sindri-onyx", body.RootElement.GetProperty("name").GetString());
        Assert.False(body.RootElement.TryGetProperty("scopes", out var scopes) && scopes.ValueKind != JsonValueKind.Null);
        Assert.False(body.RootElement.TryGetProperty("expiration_days", out var days) && days.ValueKind != JsonValueKind.Null);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Forbidden, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.Unauthorized, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.InternalServerError, "SND-ONYX-008")]
    public async Task A_token_Onyx_refuses_is_007_for_a_login_without_the_permission_and_008_otherwise(HttpStatusCode status, string code)
    {
        var session = await Session(Answering("/api/user/pats", status, "{}"));

        Assert.Equal(code, (await session.CreateToken("k", Token)).Error.Code);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not json")]
    [InlineData("{\"token\":\"\"}")]
    [InlineData("{}")]
    public async Task A_token_answer_without_a_token_is_sindri_onyx_008(string body)
    {
        var session = await Session(Answering("/api/user/pats", HttpStatusCode.OK, body));

        var token = await session.CreateToken("k", Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, token.Error.Code);
        Assert.Contains("didn't make the token", token.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_calls_that_change_a_user_take_no_email_so_nothing_can_point_them_elsewhere()
    {
        var methods = typeof(IOnyxSession).GetMethods().Where(static m => new[] { "RetireLegacyUser", "ResetDedicatedPassword", "RegisterDedicatedUser", "MakeDedicatedAdmin" }.Contains(m.Name, StringComparer.Ordinal)).ToList();

        Assert.All(methods, static m => Assert.DoesNotContain(m.GetParameters(), static p => p.ParameterType == typeof(string) && p.Name!.Contains("mail", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(4, methods.Count);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_user_changing_requests_only_ever_name_the_dedicated_or_the_retired_email()
    {
        var script = new Script { Answer = static sent => sent.PathAndQuery == "/api/password/reset_password" ? (HttpStatusCode.OK, "{\"new_password\":\"p\"}") : (HttpStatusCode.OK, "{}") };
        var session = await Session(script);

        _ = await session.RetireLegacyUser(Token);
        _ = await session.ResetDedicatedPassword(Token);
        _ = await session.RegisterDedicatedUser("pw", Token);
        _ = await session.MakeDedicatedAdmin(Token);

        var emails = script.Requests
            .Where(static r => r.Body.Contains("email", StringComparison.Ordinal))
            .Select(static r => JsonDocument.Parse(r.Body).RootElement.EnumerateObject().First(static p => p.Name.Contains("email", StringComparison.Ordinal)).Value.GetString())
            .ToList();
        Assert.Equal(["sindri@sindri.app", "sindri@example.com", "sindri@example.com", "sindri@example.com"], emails);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_token_is_the_dedicated_admin_only_for_that_users_email_with_the_admin_permission()
    {
        var accounts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Bearer good"] = "{\"account_type\":\"STANDARD\",\"email\":\"sindri@example.com\",\"effective_permissions\":[\"basic\",\"admin\"]}",
            ["Bearer basic"] = "{\"account_type\":\"STANDARD\",\"email\":\"sindri@example.com\",\"effective_permissions\":[\"basic\"]}",
            ["Bearer human"] = "{\"account_type\":\"STANDARD\",\"email\":\"someone@example.net\",\"effective_permissions\":[\"admin\"]}",
            ["Bearer noemail"] = "{\"account_type\":\"STANDARD\",\"effective_permissions\":[\"admin\"]}",
        };
        var script = new Script { Answer = sent => accounts.TryGetValue(sent.Authorization ?? string.Empty, out var body) ? (HttpStatusCode.OK, body) : (HttpStatusCode.Unauthorized, "{}") };
        var adapter = Adapter(script);

        Assert.True((await adapter.TokenIsAdmin(3000, "good", OnyxAccounts.DedicatedEmail, Token)).Value);
        Assert.False((await adapter.TokenIsAdmin(3000, "basic", OnyxAccounts.DedicatedEmail, Token)).Value);
        Assert.False((await adapter.TokenIsAdmin(3000, "human", OnyxAccounts.DedicatedEmail, Token)).Value);
        Assert.False((await adapter.TokenIsAdmin(3000, "noemail", OnyxAccounts.DedicatedEmail, Token)).Value);
        Assert.False((await adapter.TokenIsAdmin(3000, "stranger", OnyxAccounts.DedicatedEmail, Token)).Value);
        Assert.All(script.Requests, static r => Assert.Equal("/api/me", r.PathAndQuery));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_request_Onyx_does_not_answer_in_time_names_the_wait()
    {
        var script = new Script { Hang = static sent => sent.PathAndQuery == "/api/me" };
        var adapter = new HttpOnyxProvisioning(() => script, requestTimeout: TimeSpan.FromMilliseconds(100));

        var result = await adapter.TokenIsAdmin(3000, "k", OnyxAccounts.DedicatedEmail, Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Contains("didn't answer GET /api/me", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_Onyx_that_cannot_be_reached_is_an_error_not_a_false()
    {
        var script = new Script { Refuse = static _ => true };

        var admin = await Adapter(script).TokenIsAdmin(3000, "k", OnyxAccounts.DedicatedEmail, Token);
        var login = await Adapter(script).SignIn(3000, "a@b.c", "x", Token);

        Assert.Contains("couldn't be reached", admin.Error.Message, StringComparison.Ordinal);
        Assert.Contains("couldn't be reached", login.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Requests_go_to_loopback_only()
    {
        var script = new Script();
        var uris = new List<Uri>();
        var recording = new HttpOnyxProvisioning(() => new Capture(uris, script));

        _ = await recording.TokenIsAdmin(3456, "k", OnyxAccounts.DedicatedEmail, Token);

        Assert.Equal(new Uri("http://127.0.0.1:3456/api/me"), Assert.Single(uris));
    }

    private sealed class Capture(List<Uri> uris, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            uris.Add(request.RequestUri!);
            return base.SendAsync(request, cancellationToken);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_refused_login_names_no_part_of_the_password_and_the_password_goes_only_in_the_login_request_body()
    {
        const string Typed = "typed-login-value-31c7e2";
        var script = new Script { Answer = static sent => (HttpStatusCode.BadRequest, "{\"detail\": \"LOGIN_BAD_CREDENTIALS " + sent.Body + "\"}") };

        var refused = await Adapter(script).SignIn(3000, "owner@example.com", Typed, Token);

        Assert.True(refused.IsError);
        Assert.DoesNotContain(Typed, refused.Error.Message, StringComparison.Ordinal);   // Onyx's own answer here quotes the request: it is never passed on
        Assert.DoesNotContain(Typed, refused.Error.ToString(), StringComparison.Ordinal);
        var login = Assert.Single(script.Requests, static r => string.Equals(r.PathAndQuery, "/api/auth/login", StringComparison.Ordinal));
        Assert.Contains(Typed, login.Body, StringComparison.Ordinal);   // the one place it goes: a request body
        Assert.DoesNotContain(Typed, string.Concat(script.Requests.Select(static r => r.PathAndQuery + r.Authorization)), StringComparison.Ordinal);
        Assert.DoesNotContain(Typed, string.Concat(script.Requests.Where(static r => !string.Equals(r.PathAndQuery, "/api/auth/login", StringComparison.Ordinal)).Select(static r => r.Body)), StringComparison.Ordinal);
    }
}
