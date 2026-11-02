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

    [Fact, Trait("Lane", "unit")]
    public void One_command_row_serves_every_recipe_verb_with_the_verb_in_the_payload()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "ApplyCommand:command");

        Assert.Equal(
            ("command", ActionClass.Desk, false, false, false, "POST", "/sindri/workflow/instances/{instanceId}/commands", "sindri workflow command <address> <verb>"),
            (row.Variant, row.Class, row.NeedsTerminal, row.Destructive, row.ReadsViews, row.Method, row.Route, row.Cli.Text));
    }

    private static readonly (string Action, ActionClass Class, string Ui)[] _expected =
    [
        ("RegisterRecipe", ActionClass.Desk, "ready"),
        ("ApproveRecipe", ActionClass.Protected, "ready"),
        ("RevokeRecipe", ActionClass.Protected, "ready"),
        ("IssueChallenge", ActionClass.Issue, "ready"),
        ("StartInstance", ActionClass.Desk, "ready"),
        ("RerunInstance", ActionClass.Desk, "ready"),
        ("IssueNonce", ActionClass.Issue, "ready"),
        ("ApplyCommand:answer-text", ActionClass.Desk, "ready"),
        ("ApplyCommand:answer-resume", ActionClass.Desk, "ready"),
        ("ApplyCommand:answer-reject", ActionClass.Desk, "ready"),
        ("ApplyCommand:answer-grant", ActionClass.Protected, "ready"),
        ("ApplyCommand:answer-grant-tokens", ActionClass.Protected, "ready"),
        ("ApplyCommand:answer-accept", ActionClass.Protected, "ready"),
        ("ApplyCommand:takeover", ActionClass.Pause, "ready"),
        ("ApplyCommand:handback", ActionClass.Desk, "ready"),
        ("ApplyCommand:rewind", ActionClass.Desk, "ready"),
        ("ApplyCommand:cancel", ActionClass.Desk, "ready"),
        ("ApplyCommand:command", ActionClass.Desk, "ready"),
        ("CompleteStep", ActionClass.Desk, "ready"),
    ];
}
