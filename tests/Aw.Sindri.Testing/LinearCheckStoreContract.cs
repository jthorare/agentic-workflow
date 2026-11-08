using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Core.Linear;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every <see cref="ILinearCheckStore"/> honours: nothing before a record, each side keeps its own last check, a later record replaces the earlier, a write clear leaves the read check, and a clear with none is not an error.
/// Each method takes a fresh, empty store; the fake and the file adapter run them from public test classes (the port is internal, so the contract is not a public base class).
/// </summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because the port is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can not see.")]
internal static class LinearCheckStoreContract
{
    private static readonly LinearStoredCheck _read = new("keychain:sindri-index/linear", "resolves", DateTimeOffset.UnixEpoch);
    private static readonly LinearStoredCheck _write = new("keychain:sindri-index/linear-write", "scopeUnknown", DateTimeOffset.UnixEpoch.AddMinutes(5));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Nothing is checked before the first record.</summary>
    /// <param name="store">A fresh store.</param>
    /// <returns>A task.</returns>
    internal static async Task ReadBeforeAnyRecordIsTwoNulls(ILinearCheckStore store)
    {
        var read = await store.Read(Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(new LinearStoredChecks(Read: null, Write: null), read.Value);
    }

    /// <summary>Each side keeps its own check.</summary>
    /// <param name="store">A fresh store.</param>
    /// <returns>A task.</returns>
    internal static async Task EachSideKeepsItsOwnCheck(ILinearCheckStore store)
    {
        _ = await store.RecordRead(_read, Token);
        _ = await store.RecordWrite(_write, Token);

        Expect.Equal(new LinearStoredChecks(_read, _write), (await store.Read(Token)).Value);
    }

    /// <summary>A later record replaces the earlier one.</summary>
    /// <param name="store">A fresh store.</param>
    /// <returns>A task.</returns>
    internal static async Task ALaterRecordReplacesTheEarlier(ILinearCheckStore store)
    {
        _ = await store.RecordWrite(_write, Token);
        var later = _write with { At = _write.At.AddHours(1) };
        _ = await store.RecordWrite(later, Token);

        Expect.Equal(later, (await store.Read(Token)).Value.Write);
    }

    /// <summary>A clear forgets the write check only, and none to clear is fine.</summary>
    /// <param name="store">A fresh store.</param>
    /// <returns>A task.</returns>
    internal static async Task ClearingTheWriteCheckLeavesTheReadCheck(ILinearCheckStore store)
    {
        Expect.True((await store.ClearWrite(Token)).IsSuccess);
        _ = await store.RecordRead(_read, Token);
        _ = await store.RecordWrite(_write, Token);

        Expect.True((await store.ClearWrite(Token)).IsSuccess);

        Expect.Equal(new LinearStoredChecks(_read, Write: null), (await store.Read(Token)).Value);
    }
}
