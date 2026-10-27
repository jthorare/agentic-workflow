using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The shell's, Agents page's and Context page's words (owner feedback F3/F4): a user who has never read the spec understands each one.
/// These pin the words so a rename back to an internal term fails here.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PlainWordsTests
{
    [Fact]
    public void The_navigation_and_the_unserved_words_are_plain() =>
        Assert.Equal(("Workflow types", "coming soon", "coming soon", "Refreshing the lists", "Lists are up to date"),
            (Labels.Recipes, Labels.NotServedYet, Labels.NotInApp, Labels.ViewsRebuilding, Labels.ViewsRecovered));

    [Fact]
    public void The_agents_page_words_are_plain() =>
        Assert.Equal(
            ("Not part of a workflow", "Agent session", "Any status", "Watch the agent", "Coming soon", "3 agents · 1 asking you", "1 agent · 0 asking you", "Lane sindri/ideas"),
            (Labels.OutsideWorkflow, Labels.LaunchLine, Labels.AllStates, Labels.FocusLabel, Labels.FocusSoon, Labels.AgentsHead(3, 1), Labels.AgentsHead(1, 0), Labels.LaneLine("sindri", "ideas")));

    [Fact]
    public void The_session_sheet_labels_are_plain() =>
        Assert.Equal(
            ["Agent session", "Run", "Step", "Open the run", "In this status since", "How this was detected", "History", "Details"],
            [Labels.SheetLaunch, Labels.SheetInstance, Labels.SheetStep, Labels.OpenStepFrame, Labels.SheetSince, Labels.SheetStateSource, Labels.SheetHistory, Labels.SheetDetails]);

    [Fact]
    public void The_context_page_words_are_plain() =>
        Assert.Equal(
            ["Ask or look up", "Ask a question, or type a topic such as feature:name", "No context map yet", "1 topic", "4 topics", "From your question: 1 topic", "From your question: 3 topics", "Why we're doing this", "What it touches in the product", "What it touches in the code", "Suggested by AI, not confirmed"],
            [Labels.ContextAnchor, Labels.ContextAnchorHint, Labels.ContextNoMap, Labels.ContextHead(1), Labels.ContextHead(4), Labels.ContextFromQuestion(1), Labels.ContextFromQuestion(3), Labels.ContextMapBandIntent, Labels.ContextMapBandProduct, Labels.ContextMapBandCode, Labels.ContextMapLegendSuggested]);

    [Fact]
    public void The_context_page_calls_an_anchor_a_topic_everywhere_it_is_read() =>
        Assert.Equal("topic", Labels.ContextAnchorTag);

    [Fact]
    public void The_shortcut_sheet_names_pages_and_actions_in_plain_words()
    {
        var go = Labels.FixedKeys.Single(k => k.Keys.StartsWith("G then", StringComparison.Ordinal));

        Assert.Equal("Go to a page: H or N Home, D Decisions on Home, W Workflows, Q Work, R Workflow types, C Context, A Agents, . or S System, J Jobs, U Settings", go.Action);
        Assert.Contains(("O", "Open the linked page"), Labels.FixedKeys);
        Assert.Contains(("Y", "Copy the row's command"), Labels.FixedKeys);
        Assert.Contains(("X / Shift+X", "Decline / cancel the run"), Labels.FixedKeys);
        Assert.Contains(("F6 / Shift+F6", "Next / previous area"), Labels.FixedKeys);
    }
}
