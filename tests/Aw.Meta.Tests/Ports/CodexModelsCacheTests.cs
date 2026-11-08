using Aw.Kernel.Testing;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class CodexModelsCacheTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    [Fact]
    public async Task The_slugs_of_the_models_array_are_read_and_a_missing_or_damaged_file_checks_nothing()
    {
        var path = Path.Combine(_dir.Path, "models_cache.json");
        var cache = new CodexModelsCache(path);

        Assert.Null((await cache.Known(TestContext.Current.CancellationToken)).Value);

        await File.WriteAllTextAsync(path, """{"fetched_at":"x","models":[{"slug":"gpt-5.5","display_name":"GPT-5.5"},{"slug":"gpt-5.4"},{"display_name":"no slug"}]}""", TestContext.Current.CancellationToken);
        var known = (await cache.Known(TestContext.Current.CancellationToken)).Value!;

        Assert.Equal(["gpt-5.4", "gpt-5.5"], known.Order(StringComparer.Ordinal), StringComparer.Ordinal);

        await File.WriteAllTextAsync(path, "not json", TestContext.Current.CancellationToken);

        Assert.Null((await cache.Known(TestContext.Current.CancellationToken)).Value);
    }

    public void Dispose() => _dir.Dispose();
}
