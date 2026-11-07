using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The sheet's text is plain (Linear hub spec §3, "Issue sheet"): http(s) links open in the browser with the host shown, images are never fetched, and sindri:// text is not a link.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearIssueSheetTextTests
{
    private readonly LinearIssueSheetFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_web_link_in_the_description_is_a_link_that_opens_in_the_browser_and_shows_its_host()
    {
        var sheet = await _f.Opened("ENG-12", Token);

        var links = sheet.Description.SelectMany(l => l.Runs).Where(r => r.IsLink).ToList();
        await links[0].Open(Token);

        Assert.Equal(["https://example.com/runbooks/sync"], links.Select(l => l.Text), StringComparer.Ordinal);
        Assert.Equal(["https://example.com/runbooks/sync"], _f.Launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_image_is_never_fetched_or_linked_and_the_page_it_would_come_from_is_named()
    {
        var sheet = await _f.Opened("ENG-12", Token);

        var line = sheet.Description.First(l => l.Text.Contains("p95", StringComparison.Ordinal));

        Assert.DoesNotContain(line.Runs, r => r.IsLink && r.Text.Contains("p95", StringComparison.Ordinal));
        Assert.Contains("[image p95 from example.com]", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sindri_text_is_not_a_link_and_keeps_its_words()
    {
        var sheet = await _f.Opened("ENG-12", Token);

        var line = sheet.Description.First(l => l.Text.Contains("sindri://", StringComparison.Ordinal));

        Assert.DoesNotContain(line.Runs, r => r.IsLink);
        Assert.Equal("Old link: sindri://items/ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301", line.Text);
    }

    [Fact]
    public async Task A_named_link_shows_its_host_after_the_name_so_the_name_cannot_hide_it()
    {
        var issue = LinearIssueSheetFixture.Golden("full");
        issue["description"] = "Read [the runbook](https://evil.example.net/x) first.";
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(issue);

        var sheet = await _f.Opened("ENG-12", Token);

        var link = Assert.Single(sheet.Description.SelectMany(l => l.Runs), r => r.IsLink);
        Assert.Equal(("the runbook (evil.example.net)", "https://evil.example.net/x"), (link.Text, link.Link!.AbsoluteUri));
    }

    // A lookalike host (Cyrillic а in "аpple.com") shows as its punycode, so the host after the name can't pass for the real site.
    [Fact]
    public async Task A_lookalike_host_shows_as_punycode_after_a_name_on_a_picture_and_on_its_own()
    {
        var issue = LinearIssueSheetFixture.Golden("full");
        issue["description"] = "Sign in at [Apple](https://\u0430pple.com/login) or ![logo](https://\u0430pple.com/l.png) or https://\u0430pple.com/x";
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(issue);

        var sheet = await _f.Opened("ENG-12", Token);

        var runs = sheet.Description.SelectMany(l => l.Runs).ToList();
        var links = runs.Where(r => r.IsLink).Select(r => r.Text).ToList();
        Assert.Equal("Apple (xn--pple-43d.com)", links[0]);
        Assert.Contains("[image logo from xn--pple-43d.com]", string.Concat(runs.Select(r => r.Text)), StringComparison.Ordinal);
        Assert.Equal("https://xn--pple-43d.com/x", links[1]);
        Assert.DoesNotContain("\u0430", string.Concat(runs.Select(r => r.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_http_and_https_are_links()
    {
        var issue = LinearIssueSheetFixture.Golden("full");
        issue["description"] = "javascript:alert(1) and file:///etc/passwd and [x](ftp://host/a) and http://plain.example/ok.";
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(issue);

        var sheet = await _f.Opened("ENG-12", Token);

        var links = sheet.Description.SelectMany(l => l.Runs).Where(r => r.IsLink).ToList();
        Assert.Equal(["http://plain.example/ok"], links.Select(l => l.Link!.AbsoluteUri.TrimEnd('/')), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_comment_body_is_plain_text_with_the_same_links()
    {
        var issue = LinearIssueSheetFixture.Golden("full");
        issue["comments"]![0]!["body"] = "See https://example.com/a and ![img](https://example.com/i.png)";
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(issue);

        var sheet = await _f.Opened("ENG-12", Token);

        var runs = sheet.Comments[0].Lines.SelectMany(l => l.Runs).ToList();
        Assert.Equal(["https://example.com/a"], runs.Where(r => r.IsLink).Select(r => r.Text), StringComparer.Ordinal);
        Assert.Contains("[image img from example.com]", string.Concat(runs.Select(r => r.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_launch_shows_the_failed_line_with_the_link_to_copy()
    {
        var sheet = await _f.Opened("ENG-12", Token);
        _f.Launcher.Refuse = true;

        await sheet.Description.SelectMany(l => l.Runs).First(r => r.IsLink).Open(Token);

        Assert.Equal("Couldn't open the link: https://example.com/runbooks/sync", sheet.LinkLaunch.FailedText);
    }
}
