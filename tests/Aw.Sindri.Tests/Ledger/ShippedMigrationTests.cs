using System.Globalization;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>
/// A shipped migration never changes, comments included: its checksum is recorded when a box applies it and checked on
/// every start, so an edit stops Sindri on every box that ran it (AW-DATA-006). A new migration adds its row here when it ships.
/// </summary>
public sealed class ShippedMigrationTests
{
    private static readonly Dictionary<int, string> _shipped = new()
    {
        [1] = "e1f4f140ff1f76d1c37d67ecfc48aec02d1ac719632e4ccee1ab228aab06803e",
        [2] = "299cb519d2305edc96bf88533b1db7bde9a075da30bf1e1b391bd3480ece39ed",
        [4] = "3655e03c42c7ccfe04a8b9aa354f77abff5be436ae0eff0829a0c3f06a4bcf8c",
        [5] = "def35f8153bc9c5afc63c609d0a9447c9d30c33de9e488278bc708640e55948b",
        [6] = "9185a37c337088314b9c85724bbdf7cb9484c6dfb4534e3d6438e6c33d3bd3df",
        [7] = "c176b5be8c96a563deca3067c3ad61787a914c64b183d73fbc67f65e5ef77859",
        [8] = "e508255c703eb9b7ca9660691058b8b06dea9a698188c3a124455adf32bd9582",
        [9] = "410da89696743ee58d3bedc2171e684a10494c0f6e4b66c8951400335955095c",
        [10] = "b5f07b6f8994efed783fc4e84064ff8300e10e4fd46d52009ce26a55d6f4a0d4",
        [11] = "49037e0f08c489ec10a62ca890d84c6b5f3727e28dd1a1a29914e37ef54556a2",
    };

    [Fact, Trait("Lane", "unit")]
    public void A_shipped_ledger_migration_keeps_its_text_byte_for_byte()
    {
        // Version 3 is the outbox, built in code and pinned by LedgerFileTests.
        foreach (var (version, checksum) in _shipped)
        {
            var migration = LedgerDatabase.Migrations.Single(m => m.Version == version);
            Assert.True(string.Equals(checksum, migration.Checksum, StringComparison.Ordinal),
                $"Ledger migration {version.ToString(CultureInfo.InvariantCulture)} ({migration.Name}) changed text. A shipped migration never changes, comments included; put the change in a new migration.");
        }
    }
}
