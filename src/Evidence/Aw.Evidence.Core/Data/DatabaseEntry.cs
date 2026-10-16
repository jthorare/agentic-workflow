using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>A database the repo's targets config lets db-run read: a Postgres server, the read-only role to use and who the server must say it is.</summary>
/// <param name="Engine">'postgres'.</param>
/// <param name="Host">A TCP host (never a Unix socket directory).</param>
/// <param name="Port">The TCP port, 1 to 65535.</param>
/// <param name="Database">The database name.</param>
/// <param name="User">The role to connect as; it must be a non-superuser that can write nothing.</param>
/// <param name="PasswordEnv">The environment variable that holds the role's password.</param>
/// <param name="Disposable">True when the database is throwaway; db-run refuses an entry that is not.</param>
/// <param name="Identity">Who the server must say it is.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DatabaseEntry(
    string Engine,
    string Host,
    int Port,
    string Database,
    string User,
    string PasswordEnv,
    bool Disposable,
    ServerIdentity Identity)
{
    /// <summary>The only engine an entry may name.</summary>
    public const string Postgres = "postgres";
}
