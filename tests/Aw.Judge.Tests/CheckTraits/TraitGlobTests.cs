using Aw.Judge.Core.Traits;

namespace Aw.Judge.Tests.CheckTraits;

public sealed class TraitGlobTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("src/**", "src/a.ts", true)]
    [InlineData("src/**", "src/deep/er/a.ts", true)]
    [InlineData("src/**", "lib/a.ts", false)]
    [InlineData("**/*.ts", "a.ts", true)]
    [InlineData("**/*.ts", "src/x/a.ts", true)]
    [InlineData("*.ts", "src/a.ts", false)]
    [InlineData("src/*.ts", "src/a.ts", true)]
    [InlineData("src/*.ts", "src/x/a.ts", false)]
    [InlineData("src/?.ts", "src/a.ts", true)]
    [InlineData("src/?.ts", "src/ab.ts", false)]
    [InlineData("src/*.{ts,tsx}", "src/a.tsx", true)]
    [InlineData("src/*.{ts,tsx}", "src/a.js", false)]
    [InlineData("a.b", "aXb", false)]
    [InlineData("a{b", "a{b", true)]
    [InlineData("a}b", "a}b", true)]
    public void Matches_follows_the_glob_rules(string glob, string file, bool expected) => Assert.Equal(expected, TraitGlob.Matches(glob, file));

    [Fact, Trait("Lane", "unit")]
    public void A_trait_without_paths_covers_every_file() => Assert.True(TraitGlob.InScope(paths: null, "anything/at/all"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/{a,b", true)]
    [InlineData("src/{a,b}", false)]
    [InlineData("src/a}", false)]
    public void An_open_brace_is_found(string glob, bool open) => Assert.Equal(open, TraitGlob.HasUnclosedBrace(glob));
}
