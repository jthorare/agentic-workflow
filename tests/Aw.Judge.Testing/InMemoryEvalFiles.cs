using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory IEvalFiles fake: files are text under their path; reports go under <see cref="ReportsDirectory"/>. It passes EvalFilesContract.</summary>
public sealed class InMemoryEvalFiles : IEvalFiles
{
    /// <summary>Where the reports go (a made-up directory).</summary>
    public const string ReportsDirectory = "/judge/evals";

    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>When set, every call fails, as files that can't be used.</summary>
    public bool Unavailable { get; init; }

    /// <summary>The text of a file, or null when there is none.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text.</returns>
    public string? Text(string path) => _files.GetValueOrDefault(path);

    /// <summary>The path of every file, in no order.</summary>
    public IReadOnlyCollection<string> Paths => _files.Keys;

    /// <summary>Puts a file in place.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">Its text.</param>
    public void Put(string path, string text) => _files[path] = text;

    /// <inheritdoc />
    public ValueTask<Result<Unit>> AppendLine(string path, string line, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return ValueTask.FromResult(Results.Failure<Unit>(Down(path)));
        }

        _files[path] = _files.GetValueOrDefault(path, "") + line + "\n";
        return ValueTask.FromResult(Results.Success(Unit.Value));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<string>>> ReadLines(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(!Unavailable && _files.TryGetValue(path, out var text)
            ? Results.Success<IReadOnlyList<string>>([.. text.Split('\n').Where(l => l.Trim().Length > 0)])
            : Results.Failure<IReadOnlyList<string>>(Down(path)));

    /// <inheritdoc />
    public ValueTask<Result<string>> WriteReport(string name, string content, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return ValueTask.FromResult(Results.Failure<string>(Down(name)));
        }

        var path = ReportsDirectory + "/" + name;
        _files[path] = content;
        return ValueTask.FromResult(Results.Success(path));
    }

    private static Error Down(string path) => JudgeErrors.Invalid.With($"{path}: no such file or not usable");
}
