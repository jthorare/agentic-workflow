namespace Aw.Sindri.Core.Scoping;

/// <summary>One place a JSON value breaks a schema: where (a JSON pointer, '' for the root) and what.</summary>
/// <param name="Path">The JSON pointer.</param>
/// <param name="Message">What is wrong there.</param>
internal sealed record SchemaError(string Path, string Message);
