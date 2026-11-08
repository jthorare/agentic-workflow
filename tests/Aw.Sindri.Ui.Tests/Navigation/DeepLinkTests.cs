using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.Tests.Navigation;

/// <summary>UI spec §3.3 "Deep links inside the app".</summary>
[Lane(TestLane.Unit)]
public sealed class DeepLinkTests
{
    [Theory]
    [InlineData("home/needs-you", nameof(DeepLinkKind.HomeNeedsYou), null)]
    [InlineData("needs-you/nyr-01JC8", nameof(DeepLinkKind.NeedsYou), "nyr-01JC8")]
    [InlineData("item/ITM-01JC8", nameof(DeepLinkKind.Item), "ITM-01JC8")]
    [InlineData("decision/DEC-01JD2", nameof(DeepLinkKind.Decision), "DEC-01JD2")]
    [InlineData("service/index", nameof(DeepLinkKind.Service), "index")]
    [InlineData("service/nightly.index_build", nameof(DeepLinkKind.Service), "nightly.index_build")]
    [InlineData("job/JOB-01:2", nameof(DeepLinkKind.Job), "JOB-01:2")]
    public void Each_form_parses_and_prints_back(string text, string kind, string? subject)
    {
        Assert.True(DeepLink.TryParse(text, out var link));
        Assert.Equal(new DeepLink(Enum.Parse<DeepLinkKind>(kind), subject), link);
        Assert.Equal(text, link.ToString());
    }

    [Theory]
    [InlineData("node/feature:tabs.tab-manager", nameof(DeepLinkKind.Node), "feature:tabs.tab-manager")]
    [InlineData("system/temporal", nameof(DeepLinkKind.System), "temporal")]
    public void Deep_links_parse_node_session_system(string text, string kind, string subject)
    {
        Assert.True(DeepLink.TryParse(text, out var link));
        Assert.Equal(new DeepLink(Enum.Parse<DeepLinkKind>(kind), subject), link);
        Assert.Equal(text, link.ToString());
    }

    [Fact]
    public void The_projects_link_is_a_page_link_with_no_subject()
    {
        Assert.True(DeepLink.TryParse("projects", out var link));
        Assert.Equal(new DeepLink(DeepLinkKind.Projects, Subject: null), link);
        Assert.Equal("projects", link.ToString());
        Assert.False(DeepLink.TryParse("projects/x", out _));
        Assert.False(DeepLink.TryParse("Projects", out _));
    }

    [Fact]
    public void A_context_link_names_the_item_whose_scope_it_opens()
    {
        Assert.True(DeepLink.TryParse("context?item=ITM-01JC8", out var link));
        Assert.Equal(new DeepLink(DeepLinkKind.ContextItem, "ITM-01JC8"), link);
        Assert.Equal("context?item=ITM-01JC8", link.ToString());
        Assert.False(DeepLink.TryParse("context?item=", out _));
        Assert.False(DeepLink.TryParse("context?item=a/b", out _));
    }

    [Theory]
    [InlineData("session/claude-code/aaaaaaaa-0000-4000-8000-000000000001")]
    [InlineData("session/codex/bbbbbbbb-0000-4000-8000-000000000002")]
    [InlineData("session/cursor/pid-333-100")]
    public void Session_link_takes_a_provider_slash_id_ref(string text)
    {
        Assert.True(DeepLink.TryParse(text, out var link));
        Assert.Equal(new DeepLink(DeepLinkKind.Session, text["session/".Length..]), link);
        Assert.Equal(text, link.ToString());
    }

    [Theory]
    [InlineData("session/snd-01JE7.2")]
    [InlineData("session/claude-code/a/b")]
    [InlineData("session/claude-code/")]
    [InlineData("session//a")]
    [InlineData("session/./a")]
    [InlineData("session/claude-code/..")]
    [InlineData("session/claude-code/a b")]
    public void Session_link_refuses_one_segment_three_segments_and_dot_segments(string text) =>
        Assert.False(DeepLink.TryParse(text, out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("focus")]
    [InlineData("home")]
    [InlineData("home/queue")]
    [InlineData("home/needs-you/x")]
    [InlineData("item")]
    [InlineData("item/")]
    [InlineData("item/a/b")]
    [InlineData("item/ITM 1")]
    [InlineData("item/../x")]
    [InlineData("Item/ITM-1")]
    [InlineData("run/ITM-1")]
    [InlineData("sindri://item/ITM-1")]
    [InlineData(" item/ITM-1")]
    [InlineData("item/ITM-1\n")]
    public void Anything_else_is_not_a_link(string? text)
    {
        Assert.False(DeepLink.TryParse(text, out var link));
        Assert.Null(link);
    }

    [Fact]
    public void A_subject_longer_than_128_is_not_a_link()
    {
        Assert.True(DeepLink.TryParse("item/" + new string('a', 128), out _));
        Assert.False(DeepLink.TryParse("item/" + new string('a', 129), out _));
    }
}
