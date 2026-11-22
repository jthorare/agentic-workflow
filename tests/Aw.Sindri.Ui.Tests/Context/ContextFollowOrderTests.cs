using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page's reads after an event run one after another (#2775), and one that throws stops none after it.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextFollowOrderTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_read_that_throws_stops_none_after_it_and_its_exception_comes_out_at_the_end()
    {
        List<string> ran = [];
        Func<Uri, CancellationToken, Task>[] reads =
        [
            (_, _) =>
            {
                ran.Add("build");
                return Task.CompletedTask;
            },
            (_, _) =>
            {
                ran.Add("whole");
                throw new InvalidOperationException("the whole map broke");
            },
            async (_, _) =>
            {
                await Task.Yield();
                ran.Add("rules");
            },
            (_, _) =>
            {
                ran.Add("pins");
                return Task.CompletedTask;
            },
        ];

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => ContextPageViewModel.InOrder(reads, _api, Token));

        Assert.Equal("the whole map broke", thrown.Message);
        string[] order = ["build", "whole", "rules", "pins"];
        Assert.Equal(order, ran, StringComparer.Ordinal);
    }
}
