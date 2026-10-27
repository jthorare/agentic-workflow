namespace Aw.Context.Core.Glossary;

/// <summary>One symbol's vector and the file that defines it.</summary>
/// <param name="Path">The file's repo-relative path.</param>
/// <param name="Symbol">The symbol's name and vector.</param>
public sealed record PathVector(string Path, SymbolVector Symbol);
