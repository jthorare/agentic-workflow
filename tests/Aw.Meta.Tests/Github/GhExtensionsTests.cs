using Aw.Meta.Core.Github;

namespace Aw.Meta.Tests.Github;

public sealed class GhExtensionsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("gh stack\tgithub/gh-stack\tv1.0.0\n", true)]
    [InlineData("gh copilot\tgithub/gh-copilot\tv1.0.0\ngh stack\tgithub/gh-stack\tv1.0.0\n", true)]
    [InlineData("gh s\tsomeone/gh-stack\tv0.1\n", true)]
    [InlineData("gh copilot\tgithub/gh-copilot\tv1.0.0\n", false)]
    [InlineData("", false)]
    public void The_stack_extension_is_found_by_its_name_or_its_repo(string list, bool installed) =>
        Assert.Equal(installed, GhExtensions.HasStack(list));
}
