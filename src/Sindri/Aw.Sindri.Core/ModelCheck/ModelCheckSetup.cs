namespace Aw.Sindri.Core.ModelCheck;

/// <summary>
/// What the step runs the runner with: the copy of <c language="text">tlc-check.sh</c> installed beside sindri (null when it is missing, or <c language="text">AW_TLC_CHECK</c> names nothing)
/// and the only environment TLC gets (design §3, step 4).
/// </summary>
/// <param name="Runner">The runner's absolute path, or null when there is none.</param>
/// <param name="Path">The PATH the step saw; also what a 'cantRun' finding names.</param>
/// <param name="Home">HOME.</param>
/// <param name="StateDir">AW_STATE_DIR: <c language="text">tlc.sh</c> finds the jar under it.</param>
/// <param name="TlcSh">TLC_SH, the installed copy of tlc.sh.</param>
/// <param name="TlcHeap">TLC_HEAP, the heap cap ('2g').</param>
/// <param name="Tmp">TMPDIR, the step's own temp dir.</param>
public sealed record ModelCheckSetup(string? Runner, string Path, string Home, string StateDir, string TlcSh, string TlcHeap, string Tmp);
