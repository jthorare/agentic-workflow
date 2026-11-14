namespace Aw.Sindri.Features.ListWorkflowTemplates;

/// <summary>One template a person can start a workflow from.</summary>
/// <param name="Id">The template id, such as 'A'; 'wf create --template' takes it.</param>
/// <param name="Title">What the start screen shows.</param>
/// <param name="Trigger">The trigger's event kind, or 'manual' or 'schedule'.</param>
/// <param name="Steps">How many steps it has.</param>
/// <param name="Needs">What it needs before it can run, such as 'connection:github'.</param>
/// <param name="LastRunAt">When a workflow made from it last ran, or null.</param>
internal sealed record WorkflowTemplateView(string Id, string Title, string Trigger, int Steps, IReadOnlyList<string> Needs, DateTimeOffset? LastRunAt);
