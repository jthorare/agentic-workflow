namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>
/// The scoping kind's rule ids. They are schema rules only: what the files mean (citations, coverage, trust, the
/// lenses against coverage.json) stays in Sindri's ScopePacketChecks. Never PKT-SCOPE-001/002, which are the build kind's write-scope rules.
/// </summary>
public static class ScopingRules
{
    /// <summary>Every file the kind lists is present.</summary>
    public const string FilesPresent = "PKT-SCP-001";

    /// <summary>The directory holds no entry the kind doesn't list.</summary>
    public const string NoUnlisted = "PKT-SCP-002";

    /// <summary>Every JSON file is one strict JSON object, every JSON-lines file holds one object per line, brief.md is UTF-8.</summary>
    public const string WellFormed = "PKT-SCP-003";

    /// <summary>lenses.json matches its schema: version, the four lenses and their fields, ranges and enums.</summary>
    public const string LensesSchema = "PKT-SCP-004";

    /// <summary>lenses.json's lens item ids are unique and each names the lens it sits in.</summary>
    public const string LensIds = "PKT-SCP-005";

    /// <summary>graph.json (the prefiltered graph the scoping Steps worked from) has schemaVersion 1, unique non-empty node ids and edges with both ends.</summary>
    public const string GraphShape = "PKT-SCP-006";
}
