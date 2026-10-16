namespace Aw.Evidence.Core.Data;

/// <summary>A Postgres server named by a targets entry. The adapter checks who it is and what the role may do before it runs any script query.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="Password">The role's password, read from the entry's environment variable.</param>
public sealed record PostgresTarget(DatabaseEntry Entry, string Password) : DbTarget;
