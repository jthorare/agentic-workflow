namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The validator's findings in product words (#4397): each finding code maps to a sentence and a fix line that name the step by its card title. The server's own text stays for the CLI and MCP.</summary>
internal static class WorkflowFindingWords
{
    /// <summary>The name a finding about the whole workflow uses in place of a card title.</summary>
    internal const string Workflow = "This workflow";

    /// <summary>Says a finding in product words.</summary>
    /// <param name="code">The finding's code, such as GRAPH-PROMPT-001.</param>
    /// <param name="title">The step's card title, or <see cref="Workflow"/>.</param>
    /// <param name="field">The field the finding names (such as "config.prompt"), or null.</param>
    /// <returns>What is wrong and how to fix it.</returns>
    internal static (string Sentence, string Fix) Say(string code, string title, string? field) => throw new NotSupportedException();
}
