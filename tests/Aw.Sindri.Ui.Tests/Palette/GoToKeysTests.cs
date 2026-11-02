using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Palette;

[Trait("Lane", "unit")]
public sealed class GoToKeysTests
{
    [Fact]
    public void Every_letter_in_the_go_to_key_is_named_by_its_description()
    {
        var (keys, action) = Assert.Single(Labels.FixedKeys, k => k.Keys.StartsWith("G then ", StringComparison.Ordinal));
        var words = action.Split([' ', ',', ':'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var letter in keys["G then ".Length..].Split(' '))
        {
            Assert.Contains(letter, words, StringComparer.Ordinal);
        }
    }
}
