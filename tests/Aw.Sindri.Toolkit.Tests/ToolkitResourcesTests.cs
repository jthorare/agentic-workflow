using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Toolkit.Tests;

public sealed class ToolkitResourcesTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aw-toolkit-resources-");

    [Fact, Trait("Lane", "unit")]
    public void Database_cache_opens_the_file_named_by_the_SINDRI_DB_CACHE_variable()
    {
        var path = Path.Combine(_directory.FullName, "cache.db");
        using (var seed = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            seed.Open();
            using var create = seed.CreateCommand();
            create.CommandText = "create table seen(id integer primary key); insert into seen default values;";
            _ = create.ExecuteNonQuery();
        }

        var context = Context(new Dictionary<string, string>(StringComparer.Ordinal) { ["SINDRI_DB_CACHE"] = path });
        using var connection = context.Database("cache");
        using var count = connection.CreateCommand();
        count.CommandText = "select count(*) from seen";

        Assert.Equal(1L, count.ExecuteScalar());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_database_whose_variable_is_not_set_fails_naming_the_variable()
    {
        var context = Context(new Dictionary<string, string>(StringComparer.Ordinal));

        var failure = Assert.Throws<ToolkitException>(() => context.Database("cache"));

        Assert.Equal(ToolkitErrors.ResourceMissing, failure.Definition);
        Assert.Contains("SINDRI_DB_CACHE", failure.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_database_file_that_does_not_exist_fails_and_is_not_created()
    {
        var path = Path.Combine(_directory.FullName, "missing.db");
        var context = Context(new Dictionary<string, string>(StringComparer.Ordinal) { ["SINDRI_DB_CACHE"] = path });

        var failure = Assert.Throws<ToolkitException>(() => context.Database("cache"));

        Assert.Equal(ToolkitErrors.ResourceUnavailable, failure.Definition);
        Assert.False(File.Exists(path));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_resource_the_toolkit_did_not_declare_is_refused_even_when_its_variable_is_set()
    {
        var context = Context(new Dictionary<string, string>(StringComparer.Ordinal) { ["SINDRI_DB_OTHER"] = "/x" });

        var failure = Assert.Throws<ToolkitException>(() => context.Database("other"));

        Assert.Equal(ToolkitErrors.InvalidDeclaration, failure.Definition);
    }

    [Fact, Trait("Lane", "unit")]
    public void Disk_and_connection_values_come_from_their_variables()
    {
        var context = Context(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SINDRI_DISK_SCRATCH"] = _directory.FullName,
            ["SINDRI_CONNECTION_GITHUB"] = "connection-value",
        });

        Assert.Equal(_directory.FullName, context.DiskPath("scratch"));
        Assert.Equal("connection-value", context.Connection("github"));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private static ToolkitContext Context(Dictionary<string, string> environment) =>
        new(SampleToolkit.Create().ResourceNeeds, name => environment.GetValueOrDefault(name));
}
