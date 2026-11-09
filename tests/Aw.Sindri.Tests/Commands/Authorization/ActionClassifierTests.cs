using Aw.Sindri.Core.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Authorization;

/// <summary>UI spec E-12's fail-closed lookup: the row a command runs as.</summary>
public sealed class ActionClassifierTests
{
    private static readonly InMemoryCommandCatalog _catalog = new(
        Rows.Of(ActionClass.Desk, "FixturePlain", typeof(FixturePlain)),
        Rows.Of(ActionClass.Desk, "FixtureApply:answer-text", typeof(FixtureApply)),
        Rows.Of(ActionClass.Desk, "FixtureApply:answer-grant", typeof(FixtureApply)),
        Rows.Of(ActionClass.Pause, "FixtureApply:takeover", typeof(FixtureApply)));

    [Fact, Trait("Lane", "unit")]
    public void A_plain_command_takes_its_one_row() =>
        Assert.Equal("FixturePlain", ActionClassifier.Classify(_catalog, new FixturePlain()).Action.Value);

    [Fact, Trait("Lane", "unit")]
    public void Known_variant_takes_its_rows_class()
    {
        Assert.Equal(ActionClass.Pause, ActionClassifier.Classify(_catalog, new FixtureApply("takeover", Protected: false)).Class);
        Assert.Equal("FixtureApply:answer-text", ActionClassifier.Classify(_catalog, new FixtureApply("answer-text", Protected: false)).Action.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("answer-sideways")]
    [InlineData("Not A Variant")]
    public void Unknown_variant_is_classified_local(string variant)
    {
        var row = ActionClassifier.Classify(_catalog, new FixtureApply(variant, Protected: false));

        Assert.Equal(ActionClass.Local, row.Class);
        Assert.StartsWith("FixtureApply", row.Action.Value, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Protected_fields_override_the_variant()
    {
        var row = ActionClassifier.Classify(_catalog, new FixtureApply("answer-text", Protected: true));

        Assert.Equal(ActionClass.Desk, row.Class);
        Assert.False(row.NeedsTerminal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_command_with_no_row_is_classified_local() =>
        Assert.Equal(ActionClass.Local, ActionClassifier.Classify(_catalog, new FixtureUnlisted()).Class);

    private sealed record FixturePlain : ICommand<CommandAccepted>;

    private sealed record FixtureUnlisted : ICommand<CommandAccepted>;

    private sealed record FixtureApply(string Variant, bool Protected) : ICommand<CommandAccepted>, IClassifiedCommand
    {
        public bool CarriesProtectedFields => Protected;
    }
}
