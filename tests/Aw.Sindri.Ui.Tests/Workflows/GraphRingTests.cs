using Avalonia;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The selected ring sits outside the card (#3684): a 2 px ring whose inner edge is 3 px from the card's border, for every state of the card.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphRingTests
{
    [Fact]
    public async Task Selected_ring_sits_3_px_outside_on_every_state()
    {
        var card = new Rect(100, 100, 290, 60);

        var stroked = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphRingTests).Assembly).Dispatch(() => GraphCanvas.SelectedRing(card), TestContext.Current.CancellationToken);

        // The ring is drawn the same for rest, running and failed: the pen (2 px) is centred on this rectangle, so its inner edge is half the pen inside it.
        Assert.Equal(card.Inflate(4), stroked);
    }
}
