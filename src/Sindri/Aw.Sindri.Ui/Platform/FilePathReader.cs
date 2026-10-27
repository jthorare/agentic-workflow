namespace Aw.Sindri.Ui.Platform;

/// <summary>Reads a file by the path the human typed, with the same size limit as the chooser's (<see cref="StreamFilePicker"/> does the reading).</summary>
internal static class FilePathReader
{
    /// <summary>Reads one file.</summary>
    /// <param name="path">The path as typed.</param>
    /// <param name="limit">The most bytes the caller can use; a larger file is reported by its size and left unread.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file, or null when there is no such file or it can't be opened.</returns>
    internal static Task<PickedFile?> Read(string path, int limit, CancellationToken cancellationToken) =>
        new StreamFilePicker((_, _) => Task.FromResult(Chosen(path))).Pick(title: string.Empty, limit, cancellationToken);

    private static ChosenFile? Chosen(string path) =>
        File.Exists(path) ? new ChosenFile(Path.GetFileName(path), new FileInfo(path).Length, _ => Task.FromResult<Stream>(File.OpenRead(path))) : null;
}
