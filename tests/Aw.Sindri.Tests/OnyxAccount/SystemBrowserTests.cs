using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxAccount;

// The loopback-only guard of the OS opener: an address it refuses never reaches a process.
public sealed class SystemBrowserTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("https://127.0.0.1:3189/")]
    [InlineData("http://localhost:3189/")]
    [InlineData("http://example.com/")]
    [InlineData("file:///etc/passwd")]
    public void An_address_that_is_not_http_on_127_0_0_1_is_refused(string address)
        => Assert.False(new SystemBrowser().Open(new Uri(address)));

    [Fact, Trait("Lane", "unit")]
    public void A_missing_address_is_a_programming_error()
        => Assert.Throws<ArgumentNullException>(static () => new SystemBrowser().Open(null!));
}
