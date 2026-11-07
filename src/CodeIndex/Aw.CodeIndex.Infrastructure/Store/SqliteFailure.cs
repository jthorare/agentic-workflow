using Aw.CodeIndex.Core;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>Turns what SQLite throws into the expected failures of the store: a corrupt or foreign file is SND-CODEIDX-010, the rest are Aw.Kernel.Data's errors.</summary>
internal static class SqliteFailure
{
    private const int Busy = 5;
    private const int ReadOnly = 8;
    private const int IoError = 10;
    private const int Corrupt = 11;
    private const int Full = 13;
    private const int CantOpen = 14;
    private const int Constraint = 19;
    private const int NotADatabase = 26;
    private const int Permission = 3;

    /// <summary>Runs a store call and returns its failure as a <see cref="Result{T}"/> instead of an exception.</summary>
    internal static async ValueTask<Result<T>> Guard<T>(string what, Func<ValueTask<Result<T>>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (SqliteException exception) when (Map(exception, what) is { } error)
        {
            return error;
        }
        catch (InvalidDataException exception)
        {
            return CodeIndexErrors.IndexDamaged.With($"{what}: {exception.Message}");
        }
    }

    /// <summary>Re-labels a connection failure: Aw.Kernel.Data reports a file that is not a database as damaged in its own words, and this package's word is SND-CODEIDX-010.</summary>
    internal static Error Translate(Error error, string what)
        => string.Equals(error.Code, DataErrors.Damaged.Code, StringComparison.Ordinal)
            ? CodeIndexErrors.IndexDamaged.With($"{what}: {error.Message}")
            : error;

    internal static Error? Map(SqliteException exception, string what) => (exception.SqliteErrorCode & 0xFF) switch
    {
        Corrupt or NotADatabase => CodeIndexErrors.IndexDamaged.With($"{what}: {exception.Message}"),
        Busy => DataErrors.Busy.With($"{what}: {exception.Message}"),
        Constraint => DataErrors.Constraint.With($"{what}: {exception.Message}"),
        Permission or ReadOnly or IoError or Full or CantOpen => DataErrors.Unavailable.With($"{what}: {exception.Message}"),
        _ => null,
    };
}
