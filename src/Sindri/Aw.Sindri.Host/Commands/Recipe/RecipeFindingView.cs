using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>One finding in 'recipe validate --json'.</summary>
/// <param name="Code">The rule id, for example 'RCP-REACH-001'.</param>
/// <param name="Severity">error, information or skipped.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="File">The recipe file's name.</param>
/// <param name="Line">The node's line, from 1.</param>
/// <param name="Column">The node's column, from 1.</param>
/// <param name="Path">The node's path, for example 'states.orphan'.</param>
/// <param name="Fix">What to do about it.</param>
internal sealed record RecipeFindingView(string Code, FindingSeverity Severity, string Message, string File, int Line, int Column, string Path, string Fix);
