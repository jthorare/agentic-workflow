using Aw.Evidence.Core;
using Aw.Evidence.Infrastructure.Stack;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The psql adapter against a stub docker that records its arguments and standard input.</summary>
[Lane(TestLane.Integration)]
public sealed class ProvenanceQueryTests : IDisposable
{
    private static readonly DatabaseSettings _db = new(Container: "db_1", User: "app", SeedTable: "public.users");

    private readonly TempDirectory _root = new();

    [Fact]
    public async Task The_pattern_travels_as_a_psql_variable_and_never_inside_the_sql()
    {
        var pattern = "x'; DROP TABLE profile; --%";
        var query = new DockerPsqlProvenance(Recorder("printf 'seed-admin-1@example.com\\nseed-admin-2@example.com\\n'"), _db);

        var emails = await query.EmailsLikeAsync(pattern, database: null, TestContext.Current.CancellationToken);

        Assert.Equal(["seed-admin-1@example.com", "seed-admin-2@example.com"], emails.Value);
        Assert.Equal(
            ["exec", "-i", "db_1", "psql", "-U", "app", "-At", "-v", "ON_ERROR_STOP=1", "-v", "pattern=" + pattern],
            await File.ReadAllLinesAsync(_root.Combine("args"), TestContext.Current.CancellationToken));
        var sql = await File.ReadAllTextAsync(_root.Combine("stdin"), TestContext.Current.CancellationToken);
        Assert.Equal("SELECT email FROM public.users WHERE email LIKE :'pattern';\n", sql);
    }

    [Fact]
    public async Task A_worktree_slots_database_goes_to_psql_as_d_and_without_one_psql_keeps_its_default()
    {
        var query = new DockerPsqlProvenance(Recorder("echo seed-admin-1@example.com"), _db);

        _ = await query.EmailsLikeAsync("seed-admin-%@example.com", "wt_1", TestContext.Current.CancellationToken);
        var slot = await File.ReadAllLinesAsync(_root.Combine("args"), TestContext.Current.CancellationToken);
        _ = await query.AllEmailsAsync(database: null, TestContext.Current.CancellationToken);
        var main = await File.ReadAllLinesAsync(_root.Combine("args"), TestContext.Current.CancellationToken);

        Assert.Equal(["exec", "-i", "db_1", "psql", "-U", "app", "-d", "wt_1", "-At", "-v", "ON_ERROR_STOP=1", "-v", "pattern=seed-admin-%@example.com"], slot);
        Assert.Equal(["exec", "-i", "db_1", "psql", "-U", "app", "-At", "-v", "ON_ERROR_STOP=1"], main);
    }

    [Fact]
    public async Task Every_email_is_read_without_variables()
    {
        var query = new DockerPsqlProvenance(Recorder("echo a@example.com"), _db);

        Assert.Equal(["a@example.com"], (await query.AllEmailsAsync(database: null, TestContext.Current.CancellationToken)).Value);
        Assert.Equal("SELECT coalesce(email, '') FROM public.users;\n", await File.ReadAllTextAsync(_root.Combine("stdin"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_psql_failure_is_a_probe_failure_with_its_message()
    {
        var query = new DockerPsqlProvenance(Recorder("echo 'relation \"public.users\" does not exist' >&2; exit 3"), _db);

        var emails = await query.AllEmailsAsync(database: null, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.ProbeFailed.Code, emails.Error.Code);
        Assert.Contains("does not exist", emails.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "public.users", "no database container configured (set UI_EVIDENCE_DB_CONTAINER)")]
    [InlineData("db_1", null, "no seed table configured (set UI_EVIDENCE_SEED_TABLE")]
    public async Task Without_a_container_or_a_seed_table_every_query_fails_without_running_docker(string? container, string? table, string message)
    {
        var query = new DockerPsqlProvenance(_root.Combine("no-such-docker"), new DatabaseSettings(Container: container, User: "app", SeedTable: table));

        var emails = await query.AllEmailsAsync(database: null, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.ProbeFailed.Code, emails.Error.Code);
        Assert.Contains(message, emails.Error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _root.Dispose();

    private string Recorder(string then) =>
        StubCommand.Write(_root, "docker", $"printf '%s\\n' \"$@\" > '{_root.Combine("args")}'; cat > '{_root.Combine("stdin")}'; {then}");
}
