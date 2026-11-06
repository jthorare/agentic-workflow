using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Host.Skills;

/// <summary>
/// The doctor's skill rows (skill flows spec §3.2): 'skills:folder' when there is no skills folder (one row, never a failed serve), 'skills:block &lt;id&gt; is not loaded: &lt;reason&gt;' for each block that did not load, and a warning for a loaded
/// block whose block.yaml on disk now hashes differently (serve loads manifests once at start, so a recipe keeps running the old one until a restart). With every block loaded the one row says how many.
/// The row for a live skill launch with no turn-end marker after 10 minutes waits on the engine's exposure of the last marker time, and is not here yet.
/// </summary>
/// <param name="blocks">The loaded skill blocks.</param>
internal sealed class SkillsDoctorChecks(ISkillBlocks blocks) : IDoctorChecks
{
    private const string Folder = "skills:folder";
    private const string Block = "skills:block";

    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(blocks.Folder))
        {
            return Rows(new DoctorCheck(Folder, CheckState.Warn, "no skills folder", "set AW_SINDRI_SKILLS_DIR, or run ./setup.sh from the agentic-workflow checkout"));
        }

        var loaded = new SkillBlocks(blocks.Loaded, blocks.Skipped);
        var rows = new List<DoctorCheck>();
        rows.AddRange(loaded.Skipped.Select(static s => new DoctorCheck(Block, CheckState.Warn, $"{s.Name} is not loaded: {s.Reason}", $"edit {s.Path}, then restart sindri")));
        var onDisk = blocks.LoadFolder(blocks.Folder).Loaded.ToDictionary(static b => b.Manifest.Name, static b => b.ManifestHash, StringComparer.Ordinal);
        rows.AddRange(loaded.Loaded
            .Where(b => !onDisk.TryGetValue(b.Manifest.Name, out var hash) || !string.Equals(hash, b.ManifestHash, StringComparison.Ordinal))
            .Select(static b => new DoctorCheck(Block, CheckState.Warn, $"{b.Manifest.Name} changed on disk since sindri started", $"restart sindri to load it, then sindri recipe approve each recipe that uses it ({b.Path})")));
        if (rows.Count == 0)
        {
            rows.Add(new DoctorCheck(Folder, CheckState.Ok, loaded.Loaded.Count == 0 ? "no skill blocks in the folder" : $"{loaded.Loaded.Count} skill blocks loaded", Fix: null));
        }

        return Rows([.. rows]);
    }

    private static ValueTask<IReadOnlyList<DoctorCheck>> Rows(params DoctorCheck[] rows) => ValueTask.FromResult<IReadOnlyList<DoctorCheck>>(rows);
}
