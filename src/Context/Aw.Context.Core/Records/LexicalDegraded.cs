namespace Aw.Context.Core.Records;

/// <summary>Why the store's lexical leg could not answer, said instead of failing the query (spec §6.1).</summary>
/// <param name="Code">The catalogue code.</param>
/// <param name="Message">One line naming the cause and the fix; never record text.</param>
public sealed record LexicalDegraded(string Code, string Message);
