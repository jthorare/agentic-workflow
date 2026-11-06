using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Linear;

/// <summary>Sindri's marker line, and the rule that a comment is Sindri's only when its author is the write key's user too (linear hub spec 2026-11-04 §4.1, §6.2).</summary>
[Trait("Lane", "unit")]
public sealed class LinearMarkerTests
{
    private static readonly ExternalId _sindri = ExternalId.From("user-sindri");
    private static readonly ExternalId _teammate = ExternalId.From("user-teammate");

    [Fact]
    public void The_line_is_the_one_the_spec_shows() => Assert.Equal("<!-- sindri:issue=ENG-12;gen=1 -->", LinearMarker.Line("ENG-12", 1));

    [Fact]
    public void A_body_carries_the_marker_when_any_line_is_it()
    {
        var body = "Sindri run (automatic): scoping\nPR: https://example.com/pull/1\n" + LinearMarker.Line("ENG-12", 1);

        Assert.True(LinearMarker.Carries(body));
        Assert.False(LinearMarker.Carries("Looks good to me"));
        Assert.False(LinearMarker.Carries(body: null));
    }

    [Fact]
    public void A_marker_quoted_inside_a_sentence_is_not_the_marker_line() => Assert.False(LinearMarker.Carries("someone wrote " + LinearMarker.Line("ENG-12", 1) + " here"));

    [Fact]
    public void A_comment_is_sindris_when_it_carries_the_marker_and_the_write_user_wrote_it() => Assert.True(LinearMarker.IsSindri(LinearMarker.Line("ENG-12", 1), _sindri, _sindri));

    [Fact]
    public void A_teammate_pasting_the_marker_is_not_sindri() => Assert.False(LinearMarker.IsSindri(LinearMarker.Line("ENG-12", 1), _teammate, _sindri));

    [Fact]
    public void The_write_user_without_the_marker_is_not_sindri() => Assert.False(LinearMarker.IsSindri("a plain comment", _sindri, _sindri));

    [Fact]
    public void With_no_write_user_nothing_is_sindri()
    {
        Assert.False(LinearMarker.IsSindri(LinearMarker.Line("ENG-12", 1), _sindri, writeUserId: null));
        Assert.False(LinearMarker.IsSindri(LinearMarker.Line("ENG-12", 1), authorId: null, _sindri));
    }
}
