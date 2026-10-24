using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ImportMap;

public sealed class ImportMapTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_shows_the_hash_and_counts_and_writes_nothing()
    {
        var preview = await _world.Map.PreviewMap(MapWorld.MapPath, MapWorld.Human, MapWorld.None);

        Assert.Equal(new MapCounts(20, 21, 1, 1, 2), preview.Value.Counts);
        Assert.Equal((FixtureMap.Repo, MapDocumentKind.Authored), (preview.Value.Repo.Value, preview.Value.Kind));
        Assert.Matches("^[0-9a-f]{64}$", preview.Value.Hash);
        Assert.Empty((await _world.Store.Versions(MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Import_with_the_confirmed_hash_makes_the_map_current()
    {
        var imported = await _world.ImportFixture();

        Assert.Equal((1, MapVersionStatus.Current, FixtureMap.Repo), (imported.Version.Number, imported.Version.Status, imported.Version.Repo.Value));
        Assert.Equal(new MapCounts(20, 21, 1, 1, 2), imported.Counts);
        Assert.Equal(_world.Clock.GetUtcNow(), imported.Version.Created);
        Assert.Equal(Canonical.Of(Maps.Checked(Maps.Fixture()).Graph), Canonical.Of((await _world.Store.Read(1, MapWorld.None)).Value));
        Assert.Equal(2, (await _world.Store.Overrides(MapWorld.None)).Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Import_after_the_file_changed_is_SND_CONTEXT_034_and_writes_nothing()
    {
        var confirmed = (await _world.Map.PreviewMap(MapWorld.MapPath, MapWorld.Human, MapWorld.None)).Value.Hash;
        _world.Reader.Put(MapWorld.MapPath, Maps.Fixture(), FixtureMap.Yaml + "# edited\n");

        var imported = await _world.Map.ImportMap(MapWorld.MapPath, confirmed, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.MapFileChanged.Code, imported.Error.Code);
        Assert.Contains(confirmed[..12], imported.Error.Message, StringComparison.Ordinal);
        Assert.Empty((await _world.Store.Versions(MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_import_retains_the_first_and_keeps_overrides_once()
    {
        _ = await _world.ImportFixture();

        var second = await _world.ImportFixture();

        Assert.Equal(2, second.Version.Number);
        var versions = (await _world.Store.Versions(MapWorld.None)).Value;
        Assert.Equal(MapVersionStatus.Retained, versions.Single(v => v.Number == 1).Status);
        Assert.Equal(2, (await _world.Store.Overrides(MapWorld.None)).Value.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/work/repo/product-map.yaml", "SND-CONTEXT-005")]
    [InlineData("/maps/missing.yaml", "SND-CONTEXT-031")]
    public async Task A_file_that_cant_be_read_is_refused_and_nothing_is_written(string path, string code)
    {
        var imported = await _world.Map.ImportMap(path, new string('0', 64), MapWorld.Human, MapWorld.None);

        Assert.Equal(code, imported.Error.Code);
        Assert.Empty((await _world.Store.Versions(MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_invalid_map_is_refused_before_anything_is_written()
    {
        var bad = Maps.Parse("schemaVersion: 1\nkind: authored\nrepo: r\nnodes:\n  - {key: \"feature:a\", label: A}\nedges:\n  - {from: \"feature:a\", to: \"feature:b\", type: part-of}\n");
        _world.Reader.Put("/maps/bad.yaml", bad, "bad");

        var preview = await _world.Map.PreviewMap("/maps/bad.yaml", MapWorld.Human, MapWorld.None);
        var imported = await _world.Map.ImportMap("/maps/bad.yaml", new string('0', 64), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.MapNodeUndefined.Code, preview.Error.Code);
        Assert.Equal(ContextErrors.MapNodeUndefined.Code, imported.Error.Code);
        Assert.Empty((await _world.Store.Versions(MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_store_that_cant_open_is_SND_CONTEXT_004()
    {
        var hash = (await _world.Map.PreviewMap(MapWorld.MapPath, MapWorld.Human, MapWorld.None)).Value.Hash;
        _world.Store.Failure = ContextErrors.StoreUnavailable.With("AW-DATA-001: locked");

        var imported = await _world.Map.ImportMap(MapWorld.MapPath, hash, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.StoreUnavailable.Code, imported.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_for_a_repo_the_profile_doesnt_map_is_SND_CONTEXT_031_on_repo()
    {
        _world.Reader.Put("/maps/elsewhere.yaml", Maps.Fixture() with { Repo = Context.Core.Engine.ContextRepoName.From("elsewhere") }, "elsewhere");

        var preview = await _world.Map.PreviewMap("/maps/elsewhere.yaml", MapWorld.Human, MapWorld.None);
        var imported = await _world.Map.ImportMap("/maps/elsewhere.yaml", new string('0', 64), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.MapFileInvalid.Code, preview.Error.Code);
        Assert.Contains("repo: 'elsewhere' isn't a repo the approved profile maps (aw-split-panes, other)", preview.Error.Message, StringComparison.Ordinal);
        Assert.Equal(ContextErrors.MapFileInvalid.Code, imported.Error.Code);
        Assert.Empty((await _world.Store.Versions(MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_may_preview_but_not_import_a_map()
    {
        var preview = await _world.Map.PreviewMap(MapWorld.MapPath, MapWorld.Session(FixtureMap.Repo), MapWorld.None);
        var imported = await _world.Map.ImportMap(MapWorld.MapPath, new string('0', 64), MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.True(preview.IsSuccess);
        Assert.Equal(ContextErrors.SessionsDisabled.Code, imported.Error.Code);
        Assert.Empty((await _world.Store.Versions(MapWorld.None)).Value);
    }
}
