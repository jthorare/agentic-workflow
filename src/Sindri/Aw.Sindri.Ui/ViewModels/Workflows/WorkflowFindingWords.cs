namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The validator's findings in product words (#4397): each finding code maps to a sentence and a fix line that name the step by its card title. The server's own text stays for the CLI and MCP; the app keys on the code, never on the English text.</summary>
internal static class WorkflowFindingWords
{
    /// <summary>The name a finding about the whole workflow uses in place of a card title.</summary>
    internal const string Workflow = "This workflow";

    private const string ConfigPrefix = "config.";

    // {T} is the card title; {F} is ": <field>" when the finding names a field, else empty.
    private static readonly Dictionary<string, (string Sentence, string Fix)> _rows = new(StringComparer.Ordinal)
    {
        ["GRAPH-PROMPT-001"] = ("{T} has nothing to read: no step before it fetches data.", "Add a step before it, then use the output of that step in the question."),
        ["GRAPH-CONFIG-001"] = ("{T} has a wrong {N}setting.", "Open the step and correct the {N}setting."),
        ["GRAPH-TRIGGER-001"] = ("{T} is one of several starting steps, and a workflow has only one.", "Keep one starting step and delete the others."),
        ["GRAPH-TRIGGER-002"] = ("{T} has no starting step.", "Add a step that starts it, such as a schedule."),
        ["GRAPH-WIRING-001"] = ("{T} is not connected to the steps that run first.", "Connect it after another step, or delete it."),
        ["GRAPH-CYCLE-001"] = ("{T} is part of a circle of steps that never ends.", "Remove a connection so the path ends."),
        ["GRAPH-RESULT-001"] = ("{T} names a result step that is not there.", "Choose the step whose output is the result, or clear the result."),
        ["GRAPH-NODE-001"] = ("{T} has the same internal name as another step.", "Give each step its own name."),
        ["GRAPH-NODE-002"] = ("{T} uses a name that Sindri keeps for itself.", "Rename the step."),
        ["GRAPH-NODE-003"] = ("{T} has a failure setting Sindri does not know.", "Choose stop, continue or skip item."),
        ["GRAPH-NODE-004"] = ("{T} cannot carry on after a failure, because it has two exits.", "Turn off carrying on after failure, or add a step after both exits."),
        ["GRAPH-NODE-005"] = ("{T} cannot skip an item: it is not inside a loop.", "Put it between a loop and its collect step, or choose stop or continue."),
        ["GRAPH-BLOCK-001"] = ("{T} is a kind of step this Sindri does not have.", "Replace it with another kind of step."),
        ["GRAPH-EDGE-001"] = ("{T} has a connection to a step that is not there.", "Remove the connection, or add the step."),
        ["GRAPH-EDGE-002"] = ("{T} has a connection from an exit it does not have.", "Reconnect it from one of its own exits."),
        ["GRAPH-CODE-001"] = ("{T} has code that does not compile.", "Open the step and fix the code at the line it names."),
        ["GRAPH-COND-001"] = ("{T} has a condition Sindri cannot read.", "Rewrite it with comparisons, and, or, not."),
        ["GRAPH-CRON-001"] = ("{T} has a schedule that is not valid.", "Write five fields (minute, hour, day, month, weekday) and a timezone name such as America/Los_Angeles."),
        ["GRAPH-AGENT-001"] = ("{T} has no agent to run.", "Choose an agent on the step, or set a default agent in the profile."),
        ["GRAPH-AGENT-002"] = ("Sindri could not check yet which agent {T} runs on.", "Check that the step names an agent, or that a default agent is set."),
        ["GRAPH-LOOP-001"] = ("{T} is a loop that is not wired right.", "Connect the loop to exactly one collect step, and feed the steps inside it only from the loop."),
        ["GRAPH-LOOP-002"] = ("{T} ends a loop that never starts.", "Put a loop step before it, or remove it."),
        ["GRAPH-LOOP-003"] = ("{T} runs an agent inside a loop.", "Move the agent step out of the loop."),
        ["GRAPH-REPO-001"] = ("{T} names a repository that does not exist.", "Choose a repository that exists, or add it first."),
        ["GRAPH-REPO-002"] = ("Sindri could not check yet that the repository {T} uses exists.", "Check by hand that the repository exists."),
        ["GRAPH-NAME-001"] = ("{T} names a connection or memory that does not exist.", "Choose one that exists, or add it first."),
        ["GRAPH-NAME-002"] = ("Sindri could not check yet that a connection or memory {T} uses exists.", "Check by hand that it exists."),
        ["GRAPH-REF-001"] = ("{T} reads data from a step that does not run before it.", "Connect that step before this one, or read from an earlier step."),
        ["GRAPH-REF-002"] = ("{T} reads a value that does not exist.", "Pick one of the values the earlier step offers."),
        ["GRAPH-REF-003"] = ("{T} reads from a step that is not there.", "Pick a step that runs before it."),
        ["GRAPH-REF-004"] = ("{T} has a half-written reference to earlier data.", "Pick the data again, or delete the half-written part."),
        ["GRAPH-REF-005"] = ("{T} reads data of the wrong type for this field.", "Pick data of the right type, or type the value in."),
    };

    /// <summary>True when a code has its own row (a code without one gets the generic sentence).</summary>
    /// <param name="code">The finding's code.</param>
    /// <returns>Whether there is a row.</returns>
    internal static bool Knows(string code) => _rows.ContainsKey(code);

    // The codes whose own words cannot say what is wrong: a compiler error with its line, a cron, a condition, a value, a setting, a repository or connection name. Their server message stays reachable under Details.
    private static readonly HashSet<string> _withDetails = new(StringComparer.Ordinal)
    {
        "GRAPH-CODE-001", "GRAPH-CRON-001", "GRAPH-COND-001", "GRAPH-REF-002", "GRAPH-CONFIG-001", "GRAPH-REPO-001", "GRAPH-REPO-002", "GRAPH-NAME-001", "GRAPH-NAME-002",
    };

    /// <summary>True when the server's message carries what the product words cannot, so the view keeps it under Details.</summary>
    /// <param name="code">The finding's code.</param>
    /// <returns>Whether Details shows the message.</returns>
    internal static bool KeepsServerText(string code) => _withDetails.Contains(code);

    /// <summary>Says a finding in product words.</summary>
    /// <param name="code">The finding's code, such as GRAPH-PROMPT-001.</param>
    /// <param name="title">The step's card title, or <see cref="Workflow"/>.</param>
    /// <param name="field">The field the finding names (such as "config.prompt"), or null.</param>
    /// <returns>What is wrong and how to fix it; a code without a row gets a generic sentence naming the title.</returns>
    internal static (string Sentence, string Fix) Say(string code, string title, string? field)
    {
        var wholeWorkflow = string.Equals(title, Workflow, StringComparison.Ordinal);
        var (sentence, fix) = _rows.GetValueOrDefault(code, ("{T} has something to fix.", wholeWorkflow ? "Check the settings of the workflow." : "Open the step and check its settings."));
        var name = Named(field);
        return (Fill(sentence, title, name), Fill(fix, title, name));
    }

    // {T} is the title; {F} ": <field>" and {N} "<field> " (both empty when no field is named)
    private static string Fill(string text, string title, string name) =>
        text.Replace("{T}", title, StringComparison.Ordinal).Replace("{F}", name.Length > 0 ? ": " + name : string.Empty, StringComparison.Ordinal).Replace("{N}", name.Length > 0 ? name + " " : string.Empty, StringComparison.Ordinal);

    private static string Named(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return string.Empty;
        }

        var name = field.StartsWith(ConfigPrefix, StringComparison.Ordinal) ? field[ConfigPrefix.Length..] : field;
        return name.Replace('_', ' ');
    }
}
