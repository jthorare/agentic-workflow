using Aw.Sindri.Core.Workflow.Engine.Events;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>The change a notice is about (GitHub #1111): the type of its ledger event and how its banner names a stage.</summary>
/// <param name="Type">The type of the change's ledger event.</param>
/// <param name="StageName">Reads a recipe state's display name (the text names the stage by it); null names a stage by its id made readable.</param>
public sealed record NoticeChange(WorkflowEventType Type, Func<string, string?>? StageName = null);
