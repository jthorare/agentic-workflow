using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>The Setup page and the Settings choice lines say what happens (#3115): edits apply on save, so no label names an approval step, the setup.sh flag or Jev.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupCopyGuardTests
{
    private static readonly string[] _names =
    [
        "SetupRepoBody", "SetupRepoNote", "SetupSubline", "SetupHomeLine", "ReadingSetup",
        "LabelChoiceSentence", "SurfaceChoiceSentence", "ModelChoiceOff",
    ];

    [Theory]
    [InlineData("--with-sindri")]
    [InlineData("approve")]
    public void Setup_labels_do_not_name_a_removed_step(string phrase)
    {
        foreach (var (name, text) in Texts())
        {
            Assert.False(text.Contains(phrase, StringComparison.OrdinalIgnoreCase), $"{name} says \"{phrase}\": {text}");
        }
    }

    [Fact]
    public void Model_choice_off_line_does_not_name_Jev()
    {
        var text = (string)typeof(Labels).GetField("ModelChoiceOff", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        Assert.DoesNotContain("Jev", text, StringComparison.Ordinal);
    }

    private static IEnumerable<(string Name, string Text)> Texts()
    {
        foreach (var name in _names)
        {
            var field = typeof(Labels).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.True(field is not null, name + " is gone; update the guard");
            yield return (name, (string)field!.GetRawConstantValue()!);
        }
    }
}
