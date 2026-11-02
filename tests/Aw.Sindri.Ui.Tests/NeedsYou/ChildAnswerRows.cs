using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A needs-you row built from the park golden with its via set: shared with Aw.Sindri.Ui.Frames.Tests by link.</summary>
internal static class ChildAnswerRows
{
    internal static NeedsYouRowViewModel Row(string? via)
    {
        var list = Golden<NeedsYouList>("needs-you", "park");
        var served = list with { Rows = [list.Rows[0] with { Via = via }] };
        var entry = NeedsYouMapper.Map(served, Golden<CommandList>("commands", "catalog"), decisions: null, new HomeCounts(Running: 0, WithYou: 0, Queued: 0)).Rows[0];
        return new NeedsYouRowViewModel(entry, seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));
    }

    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;
}
