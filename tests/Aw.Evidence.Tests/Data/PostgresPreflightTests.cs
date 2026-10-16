using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class PostgresPreflightTests
{
    private static readonly ServerIdentity _expected = new("172.17.0.2", "fixture", 5432);
    private static readonly RoleFacts _readOnly = new("evidence_ro", Superuser: false, CreateRole: false, CreateDatabase: false, Replication: false, BypassRowSecurity: false, WritesServerFiles: false, ExecutesServerPrograms: false, WritePrivileges: 0);

    [Fact, Trait("Lane", "unit")]
    public void Identity_holds_when_address_database_and_port_agree()
        => Assert.True(PostgresPreflight.CheckIdentity(_expected, new ReportedServer("172.17.0.2", 5432, "fixture")).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void Identity_ignores_the_port_when_the_entry_names_none()
    {
        var identity = _expected with { ServerPort = null };

        Assert.True(PostgresPreflight.CheckIdentity(identity, new ReportedServer("172.17.0.2", 6543, "fixture")).IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("172.17.0.3", 5432, "fixture", "172.17.0.3")]
    [InlineData("172.17.0.2", 5432, "other", "other")]
    [InlineData("172.17.0.2", 6543, "fixture", "6543")]
    [InlineData(null, null, "fixture", "no TCP address")]
    public void Identity_mismatch_is_refused_naming_what_was_expected_and_what_the_server_said(string? address, int? port, string database, string reportedText)
    {
        var result = PostgresPreflight.CheckIdentity(_expected, new ReportedServer(address, port, database));

        Assert.Equal(EvidenceErrors.IdentityMismatch.Code, result.Error.Code);
        Assert.Contains("172.17.0.2", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(reportedText, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_role_that_can_write_nothing_is_accepted()
        => Assert.True(PostgresPreflight.CheckRole(_readOnly).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("superuser")]
    [InlineData("rolcreaterole")]
    [InlineData("rolcreatedb")]
    [InlineData("rolreplication")]
    [InlineData("rolbypassrls")]
    [InlineData("pg_write_server_files")]
    [InlineData("pg_execute_server_program")]
    [InlineData("INSERT, UPDATE, DELETE or TRUNCATE")]
    public void A_role_that_can_write_or_escape_is_refused_naming_why(string reason)
    {
        var role = reason switch
        {
            "superuser" => _readOnly with { Superuser = true },
            "rolcreaterole" => _readOnly with { CreateRole = true },
            "rolcreatedb" => _readOnly with { CreateDatabase = true },
            "rolreplication" => _readOnly with { Replication = true },
            "rolbypassrls" => _readOnly with { BypassRowSecurity = true },
            "pg_write_server_files" => _readOnly with { WritesServerFiles = true },
            "pg_execute_server_program" => _readOnly with { ExecutesServerPrograms = true },
            _ => _readOnly with { WritePrivileges = 2 },
        };

        var result = PostgresPreflight.CheckRole(role);

        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, result.Error.Code);
        Assert.Contains(reason, result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("evidence_ro", result.Error.Message, StringComparison.Ordinal);
    }
}
