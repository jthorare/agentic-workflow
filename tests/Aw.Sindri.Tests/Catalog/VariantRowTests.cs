using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// UI spec E-12 end to end: variant rows keyed '&lt;Command&gt;:&lt;variant&gt;' are listed with their own classes, and an
/// attempt runs as its variant's row, fail-closed (protected fields make it a desk row, an unknown variant a local one).
/// </summary>
public sealed class VariantRowTests
{
    private static readonly InMemoryCommandCatalog _catalog = new(
        Variant("answer-text", ActionClass.Desk),
        Variant("answer-grant", ActionClass.Desk));

    private static CallerContext App => A.Caller().Human().Client(ClientLabel.Ui).Build();

    private static CallerContext Terminal => A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    [Theory, Trait("Lane", "unit")]
    [InlineData("answer-text", false, true, true)]
    [InlineData("answer-text", true, true, true)]       // a grant field on a desk variant: a desk row
    [InlineData("answer-grant", false, true, true)]
    [InlineData("answer-sideways", false, false, false)] // an unknown variant: a local row, which nobody runs through the API (fail-closed)
    public void Variant_rows_resolve_through_the_fail_closed_rule(string variant, bool protectedFields, bool app, bool terminal)
    {
        var row = ActionClassifier.Classify(_catalog, new FixtureApply(variant, protectedFields));

        Assert.Equal(app, Authorization.Decide(row.Class, App, row).Refusal is null);
        Assert.Equal(terminal, Authorization.Decide(row.Class, Terminal, row).Refusal is null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Variant_rows_are_listed_with_their_own_classes_and_callers()
    {
        var rows = (await new ListCommandsService(_catalog).Handle(new ListCommandsRequest(), TestContext.Current.CancellationToken)).Value.Rows;

        Assert.Equal(
            [("FixtureApply:answer-grant", ActionClass.Desk, "human+terminal,human,session"), ("FixtureApply:answer-text", ActionClass.Desk, "human+terminal,human,session")],
            rows.Select(r => (r.Action.Value, r.Class, string.Join(',', r.AllowedCallers))));
    }

    private static CommandRow Variant(string variant, ActionClass actionClass) =>
        new(ActionId.From("FixtureApply:" + variant), typeof(FixtureApply), variant, actionClass, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/fixture/apply", CliForm.Parse("sindri fixture apply <instance> --" + variant).Value, "ready");
}
