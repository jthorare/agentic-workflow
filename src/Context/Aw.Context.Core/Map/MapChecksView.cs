using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Map;

/// <summary>What 'context map checks' shows: the measures of one map and the profile's mode, which decides whether a would-be refusal exits 3 or 1. It lists feature keys, so only a human reads it.</summary>
/// <param name="Source">Which map was measured.</param>
/// <param name="Version">The map version measured, or null for a build that was only extracted in memory.</param>
/// <param name="Mode">'context.checks.mode' of the approved profile.</param>
/// <param name="Report">The measures.</param>
public sealed record MapChecksView(MapChecksSource Source, int? Version, ChecksMode Mode, MapChecksReport Report);
