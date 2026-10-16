using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.DbRun;

/// <summary>A script that passed every guard before the locks: its database resolved and its goldens and seed read.</summary>
/// <param name="Script">The script.</param>
/// <param name="Target">The database to open.</param>
/// <param name="Goldens">Each query's golden text, or null where it has none.</param>
/// <param name="SeedSha256">The seed file's SHA-256 when the script seeds.</param>
/// <param name="SecretValues">Values the run resolved that must never be recorded (the role's password).</param>
internal sealed record VettedDb(DbScript Script, DbTarget Target, IReadOnlyList<string?> Goldens, string? SeedSha256, IReadOnlyList<string> SecretValues);
