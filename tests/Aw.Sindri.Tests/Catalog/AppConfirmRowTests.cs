using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The app-confirm rules (setup-once spec §4 item 7, §7.4), after owner decision q93: no row carries the flag or a confirm value. The rows the app once confirmed are gone (the profile one with the approval, the recipe ones with the engine).</summary>
public sealed class AppConfirmRowTests
{
    private static IReadOnlyList<CommandRow> Rows { get; } = new CommandCatalog().Rows;

    [Fact, Trait("Lane", "unit")]
    public void No_row_carries_the_flag_a_confirm_value_or_a_typed_confirmation_status()
    {
        Assert.All(Rows, r =>
        {
            Assert.False(r.AppConfirm, r.Action.Value);
            Assert.Null(r.Confirm);
            if (r.Action.Value != "ShowLineHoldout")   // the one row that keeps its typed challenge (group F)
            {
                Assert.NotEqual("cli-only:typed-confirmation", r.Ui, StringComparer.Ordinal);
            }
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void The_app_challenge_is_a_desk_row_for_a_human()
    {
        var row = Assert.Single(Rows, r => r.Action.Value == "IssueAppChallenge");

        Assert.Equal((typeof(IssueAppChallengeRequest), ActionClass.Desk, "POST", "/sindri/workflow/challenges/app", "ready", false, false, null),
            (row.CommandType, row.Class, row.Method, row.Route, row.Ui, row.NeedsTerminal, row.AppConfirm, row.Confirm));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_command_list_serves_confirm_null_on_every_row()
    {
        var listed = (await new ListCommandsService(new CommandCatalog()).Handle(new ListCommandsRequest(), TestContext.Current.CancellationToken)).Value.Rows;

        Assert.All(listed, r => Assert.Null(r.Confirm));
    }
}
