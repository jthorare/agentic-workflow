namespace Aw.Context.Core.Trust;

/// <summary>
/// The scrubbing ports for one call, built from the approved profile's extra patterns (spec §7, D14): the ingest
/// boundary, the egress scrubber and the fingerprint of their rules. Every call builds its own, so an approval that
/// changes the patterns applies to the next call.
/// </summary>
/// <param name="Ingest">The ingest boundary (sanitize, then scrub).</param>
/// <param name="Egress">The per-field egress scrubber.</param>
/// <param name="Ruleset">The fingerprint of the rules both apply.</param>
public sealed record Scrubbers(IIngestFilter Ingest, IEgressScrubber Egress, ScrubRuleset Ruleset);
