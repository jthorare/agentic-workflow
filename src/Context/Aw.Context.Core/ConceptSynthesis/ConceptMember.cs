namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>One record a synthesized concept groups.</summary>
/// <param name="Record">The record's id.</param>
/// <param name="Version">The record's version (source_record.version, text).</param>
/// <param name="Source">The source the record came from.</param>
public sealed record ConceptMember(string Record, string Version, string Source);
