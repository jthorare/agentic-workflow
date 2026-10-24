using System.Text.Json.Nodes;
using Aw.Judge.Infrastructure.Traits;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.CheckTraits;

public sealed class FileTraitCheckLogTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "unit")]
    public void An_entry_is_one_line_with_its_time_first()
    {
        var log = new FileTraitCheckLog(Path.Combine(_state.Path, "trait-check"), new FakeTimeProvider(new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero)));

        log.Append(new JsonObject { ["event"] = "assessment", ["p"] = 0.5 });
        log.Append(new JsonObject { ["event"] = "jev-unavailable" });

        Assert.Equal(
            ["{\"ts\":\"2026-10-23T08:00:00.000Z\",\"event\":\"assessment\",\"p\":0.5}", "{\"ts\":\"2026-10-23T08:00:00.000Z\",\"event\":\"jev-unavailable\"}"],
            File.ReadAllLines(Path.Combine(_state.Path, "trait-check", "log.jsonl")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_log_that_cannot_be_written_is_dropped()
    {
        File.WriteAllText(Path.Combine(_state.Path, "trait-check"), "a file where the directory should be");
        var log = new FileTraitCheckLog(Path.Combine(_state.Path, "trait-check"), TimeProvider.System);

        log.Append(new JsonObject { ["event"] = "assessment" });

        Assert.True(File.Exists(Path.Combine(_state.Path, "trait-check")));
    }

    public void Dispose() => _state.Dispose();
}
