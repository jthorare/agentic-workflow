using Aw.Judge.Core.Traits;
using Aw.Judge.Infrastructure.Traits;

namespace Aw.Judge.Tests.CheckTraits;

public sealed class TraitFileParserTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_trait_file_is_read_with_every_field()
    {
        const string text = "version: 1\nname: no-todo\nsubject: s\nrequirement: r\napplicability: a\npurpose: p\nguidance: g\ncontext: c\npaths: [\"src/**\", \"lib/*.ts\"]\nseverity: warn\n";

        var (trait, error) = TraitFileParser.Parse(text, "/r/.agents/traits/no-todo.yaml");

        Assert.Null(error);
        Assert.Equal(new Trait("no-todo", "s", "r", "p", TraitSeverity.Warn, "/r/.agents/traits/no-todo.yaml", "a", "g", "c", ["src/**", "lib/*.ts"]), trait, new TraitComparer());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("version: 2\nname: x\nsubject: s\nrequirement: r\npurpose: p\nseverity: block\n", "version: must be 1")]
    [InlineData("version: 1\nname: X\nsubject: s\nrequirement: r\npurpose: p\nseverity: block\n", "name: invalid")]
    [InlineData("version: 1\nname: trait-x\nsubject: s\nrequirement: r\npurpose: p\nseverity: block\n", "name: invalid")]
    [InlineData("version: 1\nname: x\nsubject: ' '\nrequirement: r\npurpose: p\nseverity: block\n", "subject: required text")]
    [InlineData("version: 1\nname: x\nsubject: s\nrequirement: r\npurpose: p\nseverity: loud\n", "severity: must be block or warn")]
    [InlineData("version: 1\nname: x\nsubject: s\nrequirement: r\npurpose: p\nseverity: block\nextra: 1\n", "trait: unrecognized key extra")]
    [InlineData("version: 1\nname: x\nsubject: s\nrequirement: r\npurpose: p\nseverity: block\npaths: []\n", "paths: must be a non-empty list")]
    [InlineData("version: 1\nname: x\nsubject: s\nrequirement: r\npurpose: p\nseverity: block\npaths: ['a/{b']\n", "paths: unclosed { in glob")]
    [InlineData("- just\n- a list\n", "trait: expected a mapping")]
    [InlineData("name: [unclosed\n", "invalid YAML")]
    public void A_bad_trait_file_is_refused_with_the_field(string text, string expectedStart)
    {
        var (trait, error) = TraitFileParser.Parse(text, "/r/x.yaml");

        Assert.Null(trait);
        Assert.StartsWith(expectedStart, error, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_name_must_match_the_file_name()
    {
        var (trait, error) = TraitFileParser.Parse("version: 1\nname: one\nsubject: s\nrequirement: r\npurpose: p\nseverity: block\n", "/r/two.yaml");

        Assert.Null(trait);
        Assert.Equal("name \"one\" must match the file name \"two.yaml\"", error);
    }

    // Trait holds a list, so records compare it by reference; compare the paths by value.
    private sealed class TraitComparer : IEqualityComparer<Trait?>
    {
        public bool Equals(Trait? x, Trait? y) => x is not null && y is not null && x with { Paths = null } == y with { Paths = null } && x.Paths!.SequenceEqual(y.Paths!, StringComparer.Ordinal);

        public int GetHashCode(Trait? obj) => obj?.Name.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
