using System.Text;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.Profile;

public sealed class YamlToJsonTests
{
    [Fact, Trait("Lane", "unit")]
    public void Maps_lists_and_core_schema_scalars_become_json()
    {
        const string yaml = "a: 1\nb: true\nc: ~\nd: hello\ne: 'true'\nf: yes\ng: [x, 2]\nh: -1.5\n";

        Assert.Null(YamlToJson.TryConvert(yaml, out var json));
        Assert.Equal("""{"a":1,"b":true,"c":null,"d":"hello","e":"true","f":"yes","g":["x",2],"h":-1.5}""", Encoding.UTF8.GetString(json));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("a: 1\na: 2\n", "line")]                         // duplicate key
    [InlineData("a: &x 1\nb: *x\n", "anchors, aliases and tags")]
    [InlineData("a: !!str 1\n", "anchors, aliases and tags")]
    [InlineData("a:\n\tb: 1\n", "line 1, column 1: While scanning")]  // a tab where indentation is expected
    [InlineData("", "empty")]
    [InlineData("a: 1\n---\nb: 2\n", "more than one")]
    public void Refuses_what_a_profile_never_needs(string yaml, string expected)
    {
        var problem = YamlToJson.TryConvert(yaml, out _);

        Assert.NotNull(problem);
        Assert.Contains(expected, problem, StringComparison.Ordinal);
    }
}
