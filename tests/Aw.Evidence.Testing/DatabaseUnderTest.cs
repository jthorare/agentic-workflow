using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Testing;

/// <summary>A database arranged by a <see cref="ReadOnlyDatabaseContract"/> subclass: the reader, the target to open and a way to see whether anything changed.</summary>
/// <param name="Reader">The reader under test.</param>
/// <param name="Target">The target to open: a database whose notes table holds (1, 'hello'), (2, 'world'), (3, 'again').</param>
/// <param name="SnapshotAsync">A text that changes when the data, a file beside it or a sequence changes (it ignores SQLite's -wal and -shm files).</param>
public sealed record DatabaseUnderTest(IDatabaseReader Reader, DbTarget Target, Func<Task<string>> SnapshotAsync);
