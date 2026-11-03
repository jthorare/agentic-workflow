namespace Aw.Sindri.Tests.Workflow.Choice;

/// <summary>RCP-CHOICE-007 (images spec 5.1): a step.judge.choose state's the images parameter, a fact key, must name a fact the state can read.</summary>
public sealed class ChoiceImagesRulesTests
{
    private const string Facts = "        facts: [fact:change]\n";

    [Fact, Trait("Lane", "unit")]
    public void An_images_fact_the_state_can_read_is_valid() =>
        Assert.Empty(RouteRecipes.Lines(RouteRecipes.Edit(RouteRecipes.Compact, Facts, Facts + "        images: \"fact:change\"\n")));

    [Fact, Trait("Lane", "unit")]
    public void An_images_fact_the_state_cannot_read_is_RCP_CHOICE_007()
    {
        var lines = RouteRecipes.Lines(RouteRecipes.Edit(RouteRecipes.Compact, Facts, Facts + "        images: \"fact:frames\"\n"));

        var own = Assert.Single(lines, l => l.StartsWith("RCP-CHOICE-007: ", StringComparison.Ordinal));
        Assert.Contains("images", own, StringComparison.Ordinal);
        Assert.Contains("fact:frames", own, StringComparison.Ordinal);
        Assert.Contains("can't read", own, StringComparison.Ordinal);
        Assert.Equal([own], lines);
    }
}
