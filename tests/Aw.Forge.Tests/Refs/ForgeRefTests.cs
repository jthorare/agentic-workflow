using Aw.Forge.Core;
using Aw.Forge.Core.Refs;

namespace Aw.Forge.Tests.Refs;

[Trait("Lane", "unit")]
public sealed class ForgeRefTests
{
    private static readonly ForgeHosts _hosts = new(["github.com", "Git.Corp.Test"]);

    [Theory]
    [InlineData("https://github.com/acme/widgets/pull/7", "github.com", "acme", "widgets", 7)]
    [InlineData("  https://GitHub.com/acme/widgets/pull/12/files  ", "github.com", "acme", "widgets", 12)]
    [InlineData("https://git.corp.test/a.b/c_d-e/pull/1", "git.corp.test", "a.b", "c_d-e", 1)]
    public void A_pull_link_parses_into_its_parts(string link, string host, string owner, string repo, int number)
    {
        var parsed = ForgeRef.Parse(link, ForgeKind.GitHub, _hosts).Value;

        Assert.Equal(new ForgeRef(ForgeKind.GitHub, host, owner, repo, number), parsed);
        Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://{host}/{owner}/{repo}/pull/{number}"), parsed.Link);
    }

    [Theory]
    [InlineData(null, "AW-FORGE-001", "link")]
    [InlineData("", "AW-FORGE-001", "link")]
    [InlineData("not a url", "AW-FORGE-001", "link")]
    [InlineData("http://github.com/acme/widgets/pull/7", "AW-FORGE-001", "scheme")]
    [InlineData("https://user:pw@github.com/acme/widgets/pull/7", "AW-FORGE-001", "credentials")]
    [InlineData("https://github.com:8443/acme/widgets/pull/7", "AW-FORGE-001", "port")]
    [InlineData("https://evil.example/acme/widgets/pull/7", "AW-FORGE-002", "evil.example")]
    [InlineData("https://github.com/acme/widgets/issues/7", "AW-FORGE-001", "path")]
    [InlineData("https://github.com/acme/widgets/pull", "AW-FORGE-001", "path")]
    [InlineData("https://github.com/ac me/widgets/pull/7", "AW-FORGE-001", "owner")]
    [InlineData("https://github.com/acme/../pull/7", "AW-FORGE-001", "repo")]
    [InlineData("https://github.com/acme/widgets/pull/0", "AW-FORGE-001", "number")]
    [InlineData("https://github.com/acme/widgets/pull/x", "AW-FORGE-001", "number")]
    public void A_bad_link_is_refused_naming_the_part_that_failed(string? link, string code, string part)
    {
        var parsed = ForgeRef.Parse(link, ForgeKind.GitHub, _hosts);

        Assert.True(parsed.IsError);
        Assert.Equal(code, parsed.Error.Code);
        Assert.Contains(part, parsed.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_host_list_allows_nothing()
    {
        var parsed = ForgeRef.Parse("https://github.com/acme/widgets/pull/7", ForgeKind.GitHub, new ForgeHosts([]));

        Assert.Equal(ForgeErrors.HostNotAllowed.Code, parsed.Error.Code);
    }
}
