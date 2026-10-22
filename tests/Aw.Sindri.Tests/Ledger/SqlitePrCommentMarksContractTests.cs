using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>'pr_comment_marks' in ledger.db (PR babysitter spec §5.4, W26) against the marks contract, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqlitePrCommentMarksContractTests : PrCommentMarksContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<LedgerDatabase> _databases = [];

    public void Dispose()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }

        _home.Dispose();
    }

    protected override IPrCommentMarks Create()
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        _databases.Add(database);
        return new SqlitePrCommentMarks(database);
    }

    [Fact]
    public async Task A_ledger_older_than_the_marks_table_has_no_marks_and_isnt_migrated_by_a_read()
    {
        var marks = Create();

        var found = await marks.Latest(new PrRef("file", "o/r", 7), TestContext.Current.CancellationToken);

        Assert.Empty(found.Value);
        Assert.False(File.Exists(Path.Combine(_home.Path, "ledger.db")));
    }
}
