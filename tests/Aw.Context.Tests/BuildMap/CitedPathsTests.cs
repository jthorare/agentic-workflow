using Aw.Context.Features.BuildMap;

namespace Aw.Context.Tests.BuildMap;

public sealed class CitedPathsTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_backticked_path_and_a_file_with_a_line_number_are_cited_once_each_in_order_of_first_mention()
    {
        var cited = CitedPaths.Find("Start at `src/billing/Invoice.cs`, then src/billing/Tax.cs:12 and `src/billing/Invoice.cs:40-44` again. Also `./README.md`.");

        Assert.Equal(["src/billing/Invoice.cs", "src/billing/Tax.cs", "README.md"], cited, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("`string.Empty` and `Foo.Bar()` are code")]
    [InlineData("See `https://example.invalid/a/b.cs` and https://example.invalid/a/b.cs:80")]
    [InlineData("`src/*.cs` is a glob, `/etc/passwd` is absolute, `../up/x.cs` leaves the repo")]
    [InlineData("and/or the version 1.2.3 at 10:30")]
    public void Prose_urls_and_things_that_are_not_repo_paths_are_not_cited(string text) => Assert.Empty(CitedPaths.Find(text));
}
