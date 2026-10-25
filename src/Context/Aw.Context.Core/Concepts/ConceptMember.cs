namespace Aw.Context.Core.Concepts;

/// <summary>One member of a concept.</summary>
/// <param name="Key">The member's node key.</param>
/// <param name="Kind">'feature', 'record' or 'decision'.</param>
/// <param name="Label">Its label as the caller reads it.</param>
public sealed record ConceptMember(string Key, string Kind, string Label);
