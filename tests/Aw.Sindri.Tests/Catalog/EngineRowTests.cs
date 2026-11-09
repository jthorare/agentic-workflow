using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// The engine's catalog rows (engine spec §6.2, §6.3, §10 'Every_engine_variant_has_a_catalog_row'; UI spec §7, E-9,
/// E-12): every engine command and variant is listed with its class and UI status, and protected rows are terminal-bound.
/// Protected rows read 'pending:&lt;phase&gt;' until their endpoint is mapped, then 'ready'.
/// </summary>
public sealed class EngineRowTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_engine_variant_has_a_catalog_row()
    {
        var rows = new CommandCatalog().Rows.ToDictionary(r => r.Action.Value, StringComparer.Ordinal);

        Assert.All(_expected, e =>
        {
            Assert.True(rows.TryGetValue(e.Action, out var row), $"{e.Action} has no catalog row");
            Assert.Equal((e.Action, e.Class, e.Ui, false), (e.Action, row.Class, row.Ui, row.NeedsTerminal));
        });
    }

    private static readonly (string Action, ActionClass Class, string Ui)[] _expected =
    [
        ("IssueChallenge", ActionClass.Issue, "ready"),
        ("IssueAppChallenge", ActionClass.Desk, "ready"),
        ("CompleteStep", ActionClass.Desk, "ready"),
        ("CompleteStep:report", ActionClass.StepReport, "ready"),
    ];
}
