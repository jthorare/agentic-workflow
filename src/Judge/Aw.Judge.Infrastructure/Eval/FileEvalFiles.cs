using System.Text;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.Eval;

/// <summary>IEvalFiles over the file system: reports go to '&lt;judge dir&gt;/evals', every file written is readable by its owner only (0600) where the OS has file modes.</summary>
/// <param name="reportsDirectory">'&lt;judge dir&gt;/evals'.</param>
internal sealed class FileEvalFiles(string reportsDirectory) : IEvalFiles
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <inheritdoc />
    public ValueTask<Result<Unit>> AppendLine(string path, string line, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Write(path, FileMode.Append, line + "\n"));

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<string>>> ReadLines(string path, CancellationToken cancellationToken)
    {
        try
        {
            return ValueTask.FromResult(Results.Success<IReadOnlyList<string>>(
                [.. File.ReadLines(path, Encoding.UTF8).Where(l => l.Trim().Length > 0)]));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<string>>(Unusable(path, exception)));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<string>> WriteReport(string name, string content, CancellationToken cancellationToken)
    {
        var path = Path.Combine(reportsDirectory, name);
        var written = Write(path, FileMode.Create, content);
        return ValueTask.FromResult(written.IsError ? Results.Failure<string>(written.Error) : Results.Success(path));
    }

    private static Result<Unit> Write(string path, FileMode mode, string text)
    {
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } directory)
            {
                _ = Directory.CreateDirectory(directory);
            }

            var options = new FileStreamOptions { Mode = mode, Access = FileAccess.Write, Share = FileShare.Read };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerOnly;
            }

            using var stream = new FileStream(path, options);
            stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
            stream.Close();

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, OwnerOnly);
            }

            return Unit.Value;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unusable(path, exception);
        }
    }

    private static Error Unusable(string path, Exception exception) =>
        Core.Errors.JudgeErrors.Invalid.With($"{path}: {exception.Message}");
}
