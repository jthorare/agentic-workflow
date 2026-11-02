using Aw.Connectors.Core;

namespace Aw.Connectors.Tests.TextCleaning;

public sealed class TextCleanerTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  \n\t ", "")]
    public void Empty_input_is_empty(string? raw, string expected) => Assert.Equal(expected, TextCleaner.Clean(raw));

    [Fact, Trait("Lane", "unit")]
    public void Html_comments_are_dropped_even_across_lines()
    {
        const string raw = "Fixes the crash.\n<!-- Please describe\nyour change above -->\nTested on Linux.";

        Assert.Equal("Fixes the crash.\nTested on Linux.", TextCleaner.Clean(raw));
    }

    [Fact, Trait("Lane", "unit")]
    public void Markdown_and_html_images_are_dropped()
    {
        const string raw = "Before ![screenshot](https://user-images.githubusercontent.com/1/a.png) after\n<img width=\"400\" alt=\"x\" src=\"https://x/y.png\">\nend";

        Assert.Equal("Before after\nend", TextCleaner.Clean(raw));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_keeps_its_text_and_the_url_when_they_differ() => Assert.Equal("see the docs (https://example.com/docs) now", TextCleaner.Clean("see [the docs](https://example.com/docs) now"));

    [Fact, Trait("Lane", "unit")]
    public void A_link_whose_text_is_its_url_keeps_it_once() => Assert.Equal("https://example.com/a", TextCleaner.Clean("[https://example.com/a](https://example.com/a)"));

    [Fact, Trait("Lane", "unit")]
    public void A_link_title_is_not_part_of_the_url() => Assert.Equal("repo (https://github.com/o/r)", TextCleaner.Clean("[repo](https://github.com/o/r \"The repo\")"));

    [Fact, Trait("Lane", "unit")]
    public void Html_anchors_keep_text_and_differing_url() => Assert.Equal("the PR (https://github.com/o/r/pull/3)", TextCleaner.Clean("<a href=\"https://github.com/o/r/pull/3\">the PR</a>"));

    [Fact, Trait("Lane", "unit")]
    public void Autolinks_lose_their_angle_brackets() => Assert.Equal("see https://example.com/x ok", TextCleaner.Clean("see <https://example.com/x> ok"));

    [Fact, Trait("Lane", "unit")]
    public void Slack_links_mentions_channels_and_specials_read_as_text()
    {
        const string raw = "<@U024BE7LH> look at <https://example.com/a|the build> and <https://example.com/b>, cc <#C024BE7LR|deploys> <!here> <!subteam^S123|@oncall>";

        Assert.Equal(
            "@U024BE7LH look at the build (https://example.com/a) and https://example.com/b, cc #deploys @here @oncall",
            TextCleaner.Clean(raw));
    }

    [Fact, Trait("Lane", "unit")]
    public void Html_tags_go_and_block_tags_break_the_line()
    {
        const string raw = "<details><summary>Logs</summary>\n<p>first</p><p>second<br>third</p></details>";

        Assert.Equal("Logs\nfirst\nsecond\nthird", TextCleaner.Clean(raw));
    }

    [Fact, Trait("Lane", "unit")]
    public void Entities_are_decoded_after_tags_are_stripped() => Assert.Equal("a < b && c > d \"q\" 'r' x y A", TextCleaner.Clean("a &lt; b &amp;&amp; c &gt; d &quot;q&quot; &#39;r&#39; x&nbsp;y &#65;"));

    [Fact, Trait("Lane", "unit")]
    public void Escaped_tag_text_stays_text() => Assert.Equal("use <script> sparingly", TextCleaner.Clean("use &lt;script&gt; sparingly"));

    [Fact, Trait("Lane", "unit")]
    public void Headings_and_bold_markers_are_removed() => Assert.Equal("Summary\nthis is important and also this", TextCleaner.Clean("## Summary\nthis is **important** and also **this**"));

    [Fact, Trait("Lane", "unit")]
    public void Code_fence_lines_go_but_the_code_stays() => Assert.Equal("run:\ndotnet build\ndone", TextCleaner.Clean("run:\n```bash\ndotnet build\n```\ndone"));

    [Fact, Trait("Lane", "unit")]
    public void Whitespace_collapses_and_control_characters_go() => Assert.Equal("a b\n\nc d", TextCleaner.Clean("a \t  b  \r\n\r\n\r\n\r\n c\u0000 \u0007d \r\n"));

    [Fact, Trait("Lane", "unit")]
    public void The_dependabot_footer_is_dropped()
    {
        const string raw = "Bumps [xunit](https://github.com/xunit/xunit) from 2.9.0 to 2.9.3.\n\n<details>\n<summary>Release notes</summary>\n<p>Notes</p>\n</details>\n\n<br/>\n\nDependabot will resolve any conflicts with this PR as long as you don't alter it yourself.\n\n[//]: # (dependabot-automerge-start)\n\n---\n\n<details>\n<summary>Dependabot commands and options</summary>\n<br />\n\nYou can trigger Dependabot actions by commenting on this PR:\n- `@dependabot rebase` will rebase this PR\n</details>";

        var cleaned = TextCleaner.Clean(raw);

        Assert.StartsWith("Bumps xunit (https://github.com/xunit/xunit) from 2.9.0 to 2.9.3.", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("Dependabot commands", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("@dependabot rebase", cleaned, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Email_reply_and_generated_by_lines_are_dropped()
    {
        const string raw = "LGTM, merging.\n\n-- \nReply to this email directly or view it on GitHub:\nhttps://github.com/o/r/pull/3#issuecomment-1\nYou are receiving this because you were mentioned.\n";

        Assert.Equal("LGTM, merging.", TextCleaner.Clean(raw));
    }

    [Fact, Trait("Lane", "unit")]
    public void Agent_attribution_lines_are_dropped()
    {
        const string raw = "Fix the retry.\n\nCo-Authored-By: Someone <a@example.com>\n\U0001F916 Generated with [Claude Code](https://claude.com/claude-code)";

        Assert.Equal("Fix the retry.", TextCleaner.Clean(raw));
    }

    [Fact, Trait("Lane", "unit")]
    public void Cleaning_is_idempotent_on_a_realistic_body()
    {
        const string raw = "<!-- template -->\n## What\nAdds **retry** to [the client](https://example.com/c).\n\n![img](https://x/y.png)\n\n- [x] tests\n";

        var once = TextCleaner.Clean(raw);

        Assert.Equal(once, TextCleaner.Clean(once));
    }
}
