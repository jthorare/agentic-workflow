using System.Globalization;
using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>The two checks made on a Postgres server before any script query: it is the one the entry names, and the role can write nothing.</summary>
public static class PostgresPreflight
{
    /// <summary>Compares who the server says it is with the entry's identity.</summary>
    /// <param name="expected">The entry's identity.</param>
    /// <param name="reported">What the server reported.</param>
    /// <returns>Success, or AW-EVIDENCE-021 naming the expected and the reported value.</returns>
    public static Result<Unit> CheckIdentity(ServerIdentity expected, ReportedServer reported)
    {
        var same = string.Equals(expected.ServerAddress, reported.Address, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expected.Database, reported.Database, StringComparison.Ordinal)
            && (expected.ServerPort is null || expected.ServerPort == reported.Port);
        return same
            ? Unit.Value
            : EvidenceErrors.IdentityMismatch.With($"expected the server at {Describe(expected.ServerAddress, expected.ServerPort, expected.Database)}, but it reports {Describe(reported.Address ?? "no TCP address", reported.Port, reported.Database)}");
    }

    /// <summary>Checks that the role is not a superuser and holds nothing that writes.</summary>
    /// <param name="role">What the server says the role may do.</param>
    /// <returns>Success, or AW-EVIDENCE-020 naming what the role may do.</returns>
    public static Result<Unit> CheckRole(RoleFacts role)
    {
        List<string> reasons = [];
        AddIf(reasons, role.Superuser, "it is a superuser");
        AddIf(reasons, role.CreateRole, "it has rolcreaterole");
        AddIf(reasons, role.CreateDatabase, "it has rolcreatedb");
        AddIf(reasons, role.Replication, "it has rolreplication");
        AddIf(reasons, role.BypassRowSecurity, "it has rolbypassrls");
        AddIf(reasons, role.WritesServerFiles, "it is a member of pg_write_server_files");
        AddIf(reasons, role.ExecutesServerPrograms, "it is a member of pg_execute_server_program");
        AddIf(reasons, role.WritePrivileges > 0, string.Create(CultureInfo.InvariantCulture, $"it can INSERT, UPDATE, DELETE or TRUNCATE in {role.WritePrivileges} tables (its own grants, PUBLIC's, or inherited through a role it is a member of)"));
        return reasons.Count == 0
            ? Unit.Value
            : EvidenceErrors.DatabaseRefused.With($"role {role.Name} is not read-only: {string.Join("; ", reasons)}. db-run needs a role that can write nothing");
    }

    private static void AddIf(List<string> reasons, bool condition, string reason)
    {
        if (condition)
        {
            reasons.Add(reason);
        }
    }

    private static string Describe(string address, int? port, string database) =>
        "address " + address + (port is null ? string.Empty : " port " + port.Value.ToString(CultureInfo.InvariantCulture)) + " database " + database;
}
