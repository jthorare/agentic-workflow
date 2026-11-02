using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Json;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests;

/// <summary>A scratch AW_STATE_DIR in its own temp directory, deleted afterwards.</summary>
public sealed class TempStateDirectory : IDisposable
{
    public TempStateDirectory()
    {
        Path = Directory.CreateTempSubdirectory("aw-judge-").FullName;
    }

    public string Path { get; }

    /// <summary>The judge database under this state directory ($AW_STATE_DIR/judge/decisions.sqlite).</summary>
    internal JudgeDatabase Database() =>
        new(JudgeDatabase.OptionsFor(System.IO.Path.Combine(Path, "judge")), new FakeTimeProvider());

    /// <summary>The SQLite decision log over <see cref="Database"/>, with the package's JSON profile.</summary>
    internal SqliteDecisionLog DecisionLog() => new(Database(), AwJson.CreateOptions(JudgeJsonContext.Default));

    /// <summary>A judge database whose directory can't be created: its parent is a regular file.</summary>
    internal JudgeDatabase UnopenableDatabase()
    {
        var file = System.IO.Path.Combine(Path, "not-a-directory");
        File.WriteAllText(file, "x");
        return new(JudgeDatabase.OptionsFor(System.IO.Path.Combine(file, "judge")), new FakeTimeProvider());
    }

    /// <summary>Copies the standard transcript fixture to projects/&lt;project&gt;/sess-1.jsonl under this directory.</summary>
    /// <param name="project">The project directory name.</param>
    /// <returns>The path of the copy.</returns>
    public string Standard(string project)
    {
        var directory = System.IO.Path.Combine(Path, "projects", project);
        Directory.CreateDirectory(directory);
        var target = System.IO.Path.Combine(directory, "sess-1.jsonl");
        File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "Transcripts", "Fixtures", "standard.jsonl"), target);
        return target;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
