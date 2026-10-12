using System.Text;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow;

public sealed class RecipeHashTests
{
    private static readonly RecipeHash _hash = RecipeHash.From("sha256:9ab2d40c1e77" + new string('0', 52));

    [Theory, Trait("Lane", "unit")]
    [InlineData("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", false)]
    [InlineData("sha256:0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef", false)]
    [InlineData("sha256:0123", false)]
    public void A_recipe_hash_is_sha256_and_64_lower_case_hex_digits(string text, bool valid) =>
        Assert.Equal(valid, RecipeHash.TryFrom(text, out _));

    [Fact, Trait("Lane", "unit")]
    public void A_hash_is_shown_as_12_hex_digits() => Assert.Equal("9ab2d40c1e77", _hash.Display);

    [Theory, Trait("Lane", "unit")]
    [InlineData("9ab2d40c", true)]
    [InlineData("sha256:9ab2d40c1e77", true)]
    [InlineData("9ab2d40", false)]
    [InlineData("9ab2d40d", false)]
    [InlineData("9AB2D40C", false)]
    public void Input_names_a_hash_by_a_prefix_of_8_or_more(string prefix, bool matches) =>
        Assert.Equal(matches, _hash.MatchesPrefix(prefix));

    [Fact, Trait("Lane", "unit")]
    public void The_hash_of_canonical_json_is_its_sha256() =>
        Assert.Equal("sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a", RecipeHash.Of(Encoding.UTF8.GetBytes("{}")).Value);
}
