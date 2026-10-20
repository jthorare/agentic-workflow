using Aw.Sindri.Features.Recipes.PromptEvals;

namespace Aw.Sindri.Tests.Recipes.PromptEvals;

public sealed class PromptChecksTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("contains", "expiry", "adds expiry to links", true)]
    [InlineData("contains", "expiry", "adds revoke", false)]
    [InlineData("notContains", "ignore previous", "scope map", true)]
    [InlineData("notContains", "ignore previous", "please ignore previous rules", false)]
    [InlineData("equals", "yes", "  yes\n", true)]
    [InlineData("equals", "yes", "yes please", false)]
    [InlineData("matches", "^AC-[0-9]+$", "AC-12", true)]
    [InlineData("matches", "^AC-[0-9]+$", "AC-x", false)]
    [InlineData("isJson", "", "{\"a\":1}", true)]
    [InlineData("isJson", "", "{oops", false)]
    public void Each_grader_decides_on_the_output_alone(string kind, string value, string output, bool expected) =>
        Assert.Equal(expected, PromptChecks.Passes(new PromptCheck(kind, value), output));
}
