namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One compile error of a 'code' node's body, as the validator reports it.</summary>
/// <param name="Line">The line in the body, counted from 1.</param>
/// <param name="Column">The column in that line, counted from 1.</param>
/// <param name="Rule">The compiler's id, such as 'CS1525'.</param>
/// <param name="Message">What is wrong.</param>
public sealed record CodeProblem(int Line, int Column, string Rule, string Message);
