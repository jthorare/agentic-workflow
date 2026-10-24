using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Decisions;

/// <summary>The pure rules of deciding (API spec §3.2 'Decide', §9; P4 plan §1.2).</summary>
[Lane(TestLane.Unit)]
public sealed class DecisionRulesTests
{
    private static readonly DecisionOption _accept = DecisionOption.From("accept");
    private readonly CommandId _command = new SequentialIdGenerator().New<CommandId>();

    [Fact]
    public void Decide_records_the_choice_the_note_and_who()
    {
        var recorded = DecisionRules.Decide(A.Decision().Build(), _accept, "go ahead", "human:ui", _command).Value;

        Expect.Equal(new DecisionRecorded(_command, A.FirstDecisionId, _accept, "go ahead", "human:ui"), recorded);
    }

    [Fact]
    public void Decide_copies_the_decisions_repo_into_the_payload()
    {
        var decision = A.Decision().About(item: null, version: 0, RepoName.From("demo")).Build();

        var recorded = DecisionRules.Decide(decision, _accept, note: null, "human:ui", _command).Value;

        Expect.Equal(RepoName.From("demo"), recorded.Repo);
    }

    [Fact]
    public void Decide_takes_the_second_option_and_no_note()
    {
        var recorded = DecisionRules.Decide(A.Decision().Build(), DecisionOption.From("decline"), note: null, "human:prover", _command).Value;

        Expect.Equal((DecisionOption.From("decline"), null), (recorded.Choice, recorded.Note));
    }

    [Fact]
    public void Decide_refuses_a_choice_outside_the_options()
    {
        var refused = DecisionRules.Decide(A.Decision().Build(), DecisionOption.From("maybe"), note: null, "human:ui", _command).Error;

        Expect.Equal(SindriErrors.DecisionInvalid.Code, refused.Code);
        Expect.True(refused.Message.Contains("accept", StringComparison.Ordinal) && refused.Message.Contains("decline", StringComparison.Ordinal));
    }

    [Fact]
    public void Decide_on_decided_is_decided_elsewhere()
    {
        var decided = A.Decision().Decided(_accept, "human:app", KnownInstants.Epoch, "decision accepted by human:app").Build();

        var refused = DecisionRules.Decide(decided, DecisionOption.From("decline"), note: null, "human:ui", _command).Error;

        Expect.Equal(SindriErrors.ItemConflict.Code, refused.Code);
        Expect.True(refused.Message.Contains("decided elsewhere", StringComparison.Ordinal));
    }

    [Fact]
    public void Decide_on_voided_is_the_item_was_closed()
    {
        var voided = A.Decision().Voided(KnownInstants.Epoch).Build();

        var refused = DecisionRules.Decide(voided, _accept, note: null, "human:ui", _command).Error;

        Expect.Equal(SindriErrors.ItemConflict.Code, refused.Code);
        Expect.True(refused.Message.Contains("the item was closed", StringComparison.Ordinal));
    }

    [Fact]
    public void Decide_refuses_a_note_over_65536_scalars()
    {
        Expect.True(DecisionRules.Decide(A.Decision().Build(), _accept, new string('n', DecisionRules.NoteMax), "human:ui", _command).IsSuccess);
        Expect.Equal(SindriErrors.DecisionInvalid.Code, DecisionRules.Decide(A.Decision().Build(), _accept, new string('n', DecisionRules.NoteMax + 1), "human:ui", _command).Error.Code);
    }
}
