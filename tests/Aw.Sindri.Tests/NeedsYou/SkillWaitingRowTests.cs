using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The 'skillWaiting' row (skill flows spec §5.4, §7; F-2a): the sentence per reason with no code or file name in it, take over as the main action, and the launch's own fields in 'ownerWaiting'.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillWaitingRowTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-11111111111111111111111111111111/step/1");
    private static readonly DateTimeOffset _since = new(2026, 11, 7, 8, 0, 0, TimeSpan.Zero);
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));

    /// <summary>Replaces a token the way the redactor does, so a row test can see the row scrubbed.</summary>
    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("ghp_SECRET", "[redacted]", StringComparison.Ordinal);
    }

    [Fact]
    public void A_question_reads_as_the_state_waiting_on_you_with_the_question()
    {
        var row = Row("question", "Who calls this API: browsers, servers, or both?");

        Expect.Equal("Shape the idea is waiting on you: Who calls this API: browsers, servers, or both?", row.Reason);
    }

    [Fact]
    public void A_run_that_stopped_without_an_answer_says_to_open_the_session_and_names_no_file()
    {
        var row = Row("noResult", text: null);

        Expect.Equal("Shape the idea stopped without giving an answer. Open the session to see why.", row.Reason);
    }

    [Fact]
    public void An_unreadable_answer_file_says_so_in_words_and_keeps_the_parse_error_off_the_sentence()
    {
        var row = Row("unreadable", text: null, detail: "SND-SKILL-001: unexpected end of JSON");

        Expect.Equal("Shape the idea finished, but its answer file is broken. Open the session and ask it to write the answer again.", row.Reason);
        Expect.Equal("SND-SKILL-001: unexpected end of JSON", row.OwnerWaiting!.Detail);
    }

    [Fact]
    public void A_skill_that_changed_since_the_run_started_names_the_skill_and_asks_for_the_approval()
    {
        var row = Row("skillUpdated", text: null);

        Expect.Equal("The review skill changed since this run started. Approve again to continue.", row.Reason);
    }

    [Fact]
    public void The_row_is_a_skillWaiting_row_of_the_instance_with_take_over_as_its_main_action()
    {
        var row = Row("question", "Which branch?");

        Expect.Equal(("skill:" + WorkflowInstanceId.Of(_address).Value, NeedsYouKind.SkillWaiting, SubjectKind.Instance, WorkflowInstanceId.Of(_address).Value), (row.Id.Value, row.Kind, row.Subject.Kind, row.Subject.Id.Value));
        Expect.Equal((ActionId.From("ApplyCommand:takeover"), "sindri takeover " + _address.Value, _since, "the item"), (row.PrimaryAction!.Value, row.Cli, row.Since, row.Title));
        Assert.Contains(row.PrimaryAction!.Value, row.Actions);
        Assert.All(row.Actions, action => Assert.Contains(action, Host.Catalog.EngineRows.All.Select(static r => r.Action)));   // every action it offers is one the catalog maps
    }

    [Fact]
    public void The_owner_waiting_object_carries_the_events_fields_for_the_Details_button()
    {
        var row = Row("question", "Which branch?");

        Expect.Equal(new NeedsYouOwnerWaiting(_launch, "question", "Which branch?", "/run/.sindri-out/result-LCH.json", Detail: null, _address, "review", 2), row.OwnerWaiting);
    }

    [Fact]
    public void A_question_holding_a_token_is_scrubbed_in_the_sentence_and_in_the_object()
    {
        var row = Row("question", "Use ghp_SECRET for the push?");

        Assert.DoesNotContain("ghp_SECRET", row.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_SECRET", row.OwnerWaiting!.Text, StringComparison.Ordinal);
        Assert.Contains("for the push?", row.Reason, StringComparison.Ordinal);
    }

    private static NeedsYouRow Row(string reason, string? text, string? detail = null)
    {
        var waiting = new LaunchOwnerWaiting(_launch, ItemQueryHarness.Id('1'), reason, text, "/run/.sindri-out/result-LCH.json", detail, _address, "review", 2);
        return NeedsYouRows.ForSkillWaiting(Item(), new SkillWaitingRun(_launch, ItemQueryHarness.Id('1'), _address, "Shape the idea", "review", waiting, _since), new Redacting());
    }

    private static ItemRow Item() => new(
        ItemQueryHarness.Id('1'), "the item", RepoName.From("agentic-workflow"), ItemState.Parked, Step: "produce", Version: 2, "human:thor", _since, _since, _since,
        Link: null, Alias: null, Body: null, WaitingOn: null, Instance: _address.Value, Owner: null, Sequence: 3);
}
