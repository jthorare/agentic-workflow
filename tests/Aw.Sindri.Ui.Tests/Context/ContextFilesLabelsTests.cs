using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Files view's and the states' words (Labels.ContextFiles.cs): plain words, no em dash, and the counts read right at one and many.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextFilesLabelsTests
{
    [Fact]
    public void No_constant_of_the_files_and_state_words_has_an_em_dash()
    {
        var words = typeof(Labels).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && (f.Name.StartsWith("ContextFiles", StringComparison.Ordinal) || f.Name.StartsWith("ContextState", StringComparison.Ordinal)))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(words);
        Assert.All(words, w => Assert.DoesNotContain('—', w));
    }

    [Theory]
    [InlineData(0, 0, "None")]
    [InlineData(1, 0, "1 ticket")]
    [InlineData(0, 2, "2 decisions")]
    [InlineData(2, 1, "2 tickets · 1 decision")]
    public void Tickets_and_decisions_read_in_plain_counts(int records, int decisions, string expected) =>
        Assert.Equal(expected, Labels.ContextFilesIntent(records, decisions));

    [Theory]
    [InlineData("feature:tabs.tab-groups", "tabs.tab-groups")]
    [InlineData("action:tab-manager.restore-layout", "tab-manager.restore-layout")]
    [InlineData("nokind", "nokind")]
    public void A_feature_key_loses_its_kind_word(string key, string expected) => Assert.Equal(expected, Labels.ContextFilesFeature(key));

    [Fact]
    public void A_status_this_build_does_not_know_shows_as_served() => Assert.Equal("archived", Labels.ContextFilesStatus("archived"));
}
