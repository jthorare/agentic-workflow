using System.Text.Json.Serialization;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Runs;

/// <summary>A parity run's record, written to &lt;run-dir&gt;/parity.json (schema 'ui-evidence.parity/1'). Paths are absolute.</summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="RunId">The run.</param>
/// <param name="Host">The origin the run drove.</param>
/// <param name="Provenance">Where the data came from.</param>
/// <param name="AppBuild">The verified commit, when given.</param>
/// <param name="DesignNode">The manifest's design node.</param>
/// <param name="Viewport">The viewport.</param>
/// <param name="Frames">The frames, in manifest order.</param>
/// <param name="At">The side of the change the --at flag named (base or head), or null when it was not given; recorded only (DTC9).</param>
public sealed record ParitySummary(
    string Schema,
    RunId RunId,
    string Host,
    Provenance Provenance,
    string? AppBuild,
    string? DesignNode,
    ViewportSize Viewport,
    IReadOnlyList<ParityFrame> Frames,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DataRunAt? At = null)
{
    /// <summary>The schema this build writes and reads.</summary>
    public const string SchemaName = "ui-evidence.parity/1";

    /// <summary>The file a parity run writes in its run directory, and parity-plan and parity-comment read.</summary>
    public const string FileName = "parity.json";

    /// <summary>Every failed geometry check, with its frame.</summary>
    /// <returns>The failures, in frame order.</returns>
    public IReadOnlyList<(string Frame, Design.BoxCheck Check)> FailedBoxChecks() =>
        [.. Frames.SelectMany(static f => f.BoxChecks.Where(static c => !c.Pass).Select(c => (f.Name, c)))];
}
