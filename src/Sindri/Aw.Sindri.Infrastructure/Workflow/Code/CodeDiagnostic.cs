namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>One compile error of a Code node's body.</summary>
/// <param name="Line">The line in the body, counted from 1.</param>
/// <param name="Column">The column in that line, counted from 1.</param>
/// <param name="Id">The compiler's id, such as 'CS1525'.</param>
/// <param name="Message">What is wrong.</param>
internal sealed record CodeDiagnostic(int Line, int Column, string Id, string Message);
