namespace Aw.Evidence.Core.Data;

/// <summary>What the server says the connected role may do.</summary>
/// <param name="Name">The role's name.</param>
/// <param name="Superuser">rolsuper.</param>
/// <param name="CreateRole">rolcreaterole.</param>
/// <param name="CreateDatabase">rolcreatedb.</param>
/// <param name="Replication">rolreplication.</param>
/// <param name="BypassRowSecurity">rolbypassrls.</param>
/// <param name="WritesServerFiles">The role is a member of pg_write_server_files.</param>
/// <param name="ExecutesServerPrograms">The role is a member of pg_execute_server_program.</param>
/// <param name="WritePrivileges">How many non-system tables, views and the like the role can INSERT, UPDATE, DELETE or TRUNCATE, counting its own grants, PUBLIC's and those inherited through role membership (has_table_privilege).</param>
public sealed record RoleFacts(
    string Name,
    bool Superuser,
    bool CreateRole,
    bool CreateDatabase,
    bool Replication,
    bool BypassRowSecurity,
    bool WritesServerFiles,
    bool ExecutesServerPrograms,
    int WritePrivileges);
