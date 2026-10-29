using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Tests.OnyxAccount;

/// <summary>The login secret type (setup-once spec §5.6 "The password"): nothing prints or serializes the secret.</summary>
[Lane(TestLane.Unit)]
public sealed class OnyxLoginSecretTests
{
    private const string Typed = "typed-by-the-owner-77aa";

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
}
