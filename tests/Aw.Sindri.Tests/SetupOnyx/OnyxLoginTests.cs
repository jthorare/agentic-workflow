using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SetupOnyx;

/// <summary>The secret type and the body reader of the login route (setup-once spec §5.6 "The password"): nothing prints or serializes the secret, and a body that is wrong in any way is answered with a fixed sentence that never quotes it.</summary>
[Lane(TestLane.Unit)]
public sealed class OnyxLoginTests
{
    private const string Typed = "typed-by-the-owner-77aa";

    private static async Task<Kernel.Result<OnyxLogin>> Read(string body) =>
        await OnyxLoginBody.Read(new MemoryStream(Encoding.UTF8.GetBytes(body)), TestContext.Current.CancellationToken);

    [Fact]
    public void The_secret_and_the_login_print_nothing_of_themselves()
    {
        var login = new OnyxLogin("owner@example.com", new OnyxSecret(Typed));

        Assert.DoesNotContain(Typed, login.Password.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Typed, login.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("owner@example.com", login.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Typed, $"{login} {login.Password} {string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", login.Password)}", StringComparison.Ordinal);
    }

    [Fact]
    public void The_secret_can_not_be_written_as_JSON_by_any_options_and_can_not_be_read_either()
    {
        var options = SliceHarness.Json;

        _ = Assert.ThrowsAny<Exception>(() => JsonSerializer.Serialize(new OnyxSecret(Typed), options));
        _ = Assert.ThrowsAny<Exception>(() => AwJson.Serialize(new OnyxLogin("a@b.c", new OnyxSecret(Typed)), options));
        _ = Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<OnyxSecret>("\"" + Typed + "\"", options));
    }

    [Fact]
    public async Task A_good_body_reads_into_the_login_with_the_email_trimmed()
    {
        var login = await Read($"{{\"adminEmail\": \" owner@example.com \", \"adminPassword\": \"{Typed}\"}}");

        Assert.True(login.IsSuccess, login.IsError ? login.Error.ToString() : null);
        Expect.Equal("owner@example.com", login.Value.Email);
        Expect.Equal(Typed, login.Value.Password.Reveal());
    }

    [Theory]
    [InlineData("{\"adminEmail\": \"a@b.c\"}")]
    [InlineData("{\"adminPassword\": \"x\"}")]
    [InlineData("{\"adminEmail\": \"\", \"adminPassword\": \"x\"}")]
    [InlineData("{\"adminEmail\": \"a@b.c\", \"adminPassword\": \"\"}")]
    [InlineData("{\"adminEmail\": \"a@b.c\", \"adminPassword\": 12345}")]
    [InlineData("{\"adminEmail\": null, \"adminPassword\": null}")]
    [InlineData("{}")]
    public async Task A_body_missing_either_field_is_SND_SETUP_005(string body)
    {
        var login = await Read(body);

        Assert.True(login.IsError);
        Expect.Equal(SindriErrors.SetupOnyxCredentialsMissing.Code, login.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("not json " + Typed)]
    [InlineData("[\"" + Typed + "\"]")]
    [InlineData("\"" + Typed + "\"")]
    [InlineData("{\"adminEmail\": \"a@b.c\", \"adminPassword\": \"" + Typed + "\"")]
    [InlineData("{\"adminEmail\": \"a@b.c\", \"adminPassword\": \"" + Typed + "\", \"x\": [[[[[[1]]]]]]}")]
    public async Task A_body_that_is_not_a_JSON_object_is_a_fixed_400_that_never_quotes_the_input(string body)
    {
        var login = await Read(body);

        Assert.True(login.IsError);
        Expect.Equal(SindriErrors.InvalidInput.Code, login.Error.Code);
        Assert.DoesNotContain(Typed, login.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not json", login.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_body_over_the_limit_is_refused_with_the_same_fixed_sentence()
    {
        var login = await Read("{\"adminEmail\": \"a@b.c\", \"adminPassword\": \"" + new string('x', OnyxLoginBody.MaxBytes) + "\"}");

        Assert.True(login.IsError);
        Expect.Equal(SindriErrors.InvalidInput.Code, login.Error.Code);
    }
}
