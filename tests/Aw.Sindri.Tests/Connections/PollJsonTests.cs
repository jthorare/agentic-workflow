using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Connections.Polling;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class PollJsonTests
{
    [Fact]
    public void Long_text_is_cut_without_splitting_a_surrogate_pair()
    {
        var text = new string('a', 1999) + "\U0001F600" + "tail";   // the emoji is two chars at positions 1999 and 2000
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(text));

        var cut = PollJson.Text(document.RootElement);

        Assert.NotNull(cut);
        Assert.False(char.IsHighSurrogate(cut[^1]));
        Assert.Equal(1999, cut.Length);
    }
}
